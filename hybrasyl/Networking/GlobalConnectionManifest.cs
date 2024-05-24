using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Servers;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Hybrasyl.Networking;

public static class GlobalConnectionManifest
{
    private static long _connectionId;

    public static ConcurrentDictionary<long, IClient> ConnectedClients = new();
    public static ConcurrentDictionary<long, IClient> WorldClients = new();
    public static ConcurrentDictionary<long, Redirect> Redirects = new();

    public static long GetNewConnectionId()
    {
        Interlocked.Increment(ref _connectionId);
        return _connectionId;
    }

    public static void RegisterRedirect(IClient client, Redirect redirect)
    {
        Redirects[client.ConnectionId] = redirect;
    }

    public static bool TryGetRedirect(long cid, out Redirect redirect) => Redirects.TryGetValue(cid, out redirect);

    public static void RegisterClient(IClient client)
    {
        ConnectedClients[client.ConnectionId] = client;
        if (client.ServerType == ServerTypes.World)
            WorldClients[client.ConnectionId] = client;
    }

    public static void DeregisterClient(IClient client)
    {
        ConnectedClients.TryRemove(client.ConnectionId, out _);
        GameLog.InfoFormat("Deregistering {0}", client.ConnectionId);
        // Send a control message to clean up after World users; Lobby and Login handle themselves
        if (client.ServerType == ServerTypes.World)
        {
            if (!WorldClients.TryRemove(client.ConnectionId, out _))
                GameLog.Error("Couldn't deregister cid {id}", client.ConnectionId);
            try
            {
                if (!World.ControlMessageQueue.IsCompleted)
                    World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.CleanupUser,
                        CleanupType.ByConnectionId, client.ConnectionId));
            }
            catch (InvalidOperationException e)
            {
                Game.ReportException(e);
                if (!World.ControlMessageQueue.IsCompleted)
                    GameLog.ErrorFormat("Connection {id}: DeregisterClient failed", client.ConnectionId);
            }
        }
    }

    public static byte[] RequestEncryptionKey(string endpoint, IPAddress remoteAddress)
    {
        byte[] key;

        try
        {
            var seed = new Seed { Ip = remoteAddress.ToString() };

            var webReq = WebRequest.Create(new Uri(endpoint));
            webReq.ContentType = "application/json";
            webReq.Method = "POST";

            var json = JsonSerializer.Serialize(seed);

            using (var sw = new StreamWriter(webReq.GetRequestStream()))
            {
                sw.Write(json);
            }

            var response = webReq.GetResponse();
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                key = (byte[])JsonSerializer.Deserialize(sr.ReadToEnd(), typeof(byte[]));
            }
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error("RequestEncryptionKey failure: {e}", e);
            key = Encoding.ASCII.GetBytes("NOTVALID!");
        }

        return key;
    }

    public static bool ValidateEncryptionKey(string endpoint, ServerToken token)
    {
        bool valid;

        try
        {
            var webReq = WebRequest.Create(new Uri(endpoint));
            webReq.ContentType = "application/json";
            webReq.Method = "POST";

            var json = JsonSerializer.Serialize(token);

            using (var sw = new StreamWriter(webReq.GetRequestStream()))
            {
                sw.Write(json);
            }

            var response = webReq.GetResponse();
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                valid = (bool)JsonSerializer.Deserialize(sr.ReadToEnd(), typeof(bool));
            }
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error("ValidateEncryptionKey failure: {e}", e);
            return false;
        }

        return valid;
    }
}