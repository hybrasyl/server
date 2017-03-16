/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Scripting.Utils;

namespace Hybrasyl
{
    public class ClientState
    {
        private const int BufferSize = 1024;
        private byte[] _buffer = new byte[BufferSize];
        public bool Recieving;
        private ConcurrentQueue<ServerPacket> _sendBuffer = new ConcurrentQueue<ServerPacket>(); 
        public bool Connected { get; set; }

        public long Id { get; }

        public object Lock = new object();

        public Socket WorkSocket { get; }

        public ClientState(Socket incoming)
        {
            this.WorkSocket = incoming;
            this.Id = GlobalConnectionManifest.GetNewConnectionId();
            this.Connected = true;
        }

        public byte[] Buffer => _buffer;
        public void ReceiveBufferAdd(IEnumerable<byte> received)
        {
            lock (_buffer)
            {
                _buffer.AddRange(received);
            }
        }

        public IEnumerable<byte> ReceiveBufferTake(int range)
        {
            lock (_buffer)
            {
                return _buffer.Take(range);
            }
        }

        public IEnumerable<byte> ReceiveBufferPop(int range)
        {
            lock (_buffer)
            {
                var ret = _buffer.Take(range);
                var asList = _buffer.ToList();
                asList.RemoveRange(0, range);
                _buffer = asList.ToArray();
                return ret;
            }
        }

        public void SendBufferAdd(ServerPacket packet)
        {
            _sendBuffer.Enqueue(packet);
        }

        public bool SendBufferTake(out ServerPacket packet)
        {
            return _sendBuffer.TryDequeue(out packet);
        }

        public void ResetReceive()
        {
            lock (_buffer)
            {
                _buffer = new byte[BufferSize];
            }
        }

        public void ResetSend()
        {
            _sendBuffer = new ConcurrentQueue<ServerPacket>();
        }

        public void Dispose()
        {
            try
            {
                WorkSocket.Shutdown(SocketShutdown.Both);
                WorkSocket.Close();
            }
            catch (Exception)
            {
                WorkSocket.Close();
            }

            Connected = false;
        }
    }

    public class Client
    {

        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool Connected => ClientState.Connected;

        public ClientState ClientState;

        public Socket Socket => ClientState.WorkSocket;

        private Server Server { get; set; }

        public Dictionary<byte, ThrottleInfo> Throttle = new Dictionary<byte, ThrottleInfo>();

        public long ConnectionId => ClientState.Id;

        private long _lastReceived = 0;
        private long _lastSent = 0;
        private long _idle = 0;
        
        public byte ServerOrdinal = 0x00;
        //private byte clientOrdinal = 0x00;

        public string RemoteAddress
        {
            get
            {
                if (Socket != null)
                {
                    return ((System.Net.IPEndPoint) Socket.RemoteEndPoint).Address.ToString();
                }
                return "nil";
            }
        }

        public byte EncryptionSeed { get; set; }
        public byte[] EncryptionKey { get; set; }
        private byte[] EncryptionKeyTable { get; set; }

        public string NewCharacterName { get; set; }
        public string NewCharacterSalt { get; set; }
        public string NewCharacterPassword { get; set; }

        private int _heartbeatA = 0;
        private int _heartbeatB = 0;
        private long _byteHeartbeatSent = 0;
        private long _tickHeartbeatSent = 0;
        private long _byteHeartbeatReceived = 0;
        private long _tickHeartbeatReceived = 0;

        private int _localTickCount = 0;  // Make this int32 because it's what the client expects
        private int _clientTickCount = 0;

        public long ConnectedSince = 0;

        public byte CurrentMusicTrack { get; private set; }

        /// <summary>
        /// Return the ServerType of a connection, corresponding with Hybrasyl.Utility.ServerTypes
        /// </summary>
        public int ServerType
        {
            get
            {
                if (Server is Lobby)
                {
                    return ServerTypes.Lobby;
                }
                if (Server is Login)
                {
                    return ServerTypes.Login;
                }
                return ServerTypes.World;
            }
        }

        /// <summary>
        /// Atomically update the byte-based heartbeat values for the 0x3B packet and then
        /// queue for transmission to the client. This transmission is aborted if the client hasn't
        /// been alive more than BYTE_HEARTBEAT_INTERVAL seconds.
        /// If we don't receive a response to the 0x3B heartbeat within REAP_HEARTBEAT_INTERVAL
        /// the client is automatically disconnected.
        /// </summary>
        public void SendByteHeartbeat()
        {
            var aliveSince = new TimeSpan(DateTime.Now.Ticks - ConnectedSince);
            if (aliveSince.TotalSeconds < Constants.BYTE_HEARTBEAT_INTERVAL)
                return;
            var rnd = new Random();
            var byteHeartbeat = new ServerPacket(0x3b);
            var a = rnd.Next(254);
            var b = rnd.Next(254);
            Interlocked.Exchange(ref _heartbeatA, a);
            Interlocked.Exchange(ref _heartbeatB, b);
            byteHeartbeat.WriteByte((byte)a);
            byteHeartbeat.WriteByte((byte)b);
            Enqueue(byteHeartbeat);
            Interlocked.Exchange(ref _byteHeartbeatSent, DateTime.Now.Ticks);
        }

        /// <summary>
        /// Check to see if a client is idle
        /// </summary>
        public void CheckIdle()
        {
            var now = DateTime.Now.Ticks;
            var idletime = new TimeSpan(now - _lastReceived);
            if (idletime.TotalSeconds > Constants.IDLE_TIME)
            {
                Logger.DebugFormat("cid {0}: idle for {1} seconds, marking as idle", ConnectionId, idletime.TotalSeconds);
                ToggleIdle();
                Logger.DebugFormat("cid {0}: ToggleIdle: {1}", ConnectionId, IsIdle());
            }
            else
            {
                Logger.DebugFormat("cid {0}: idle for {1} seconds, not idle", ConnectionId, idletime.TotalSeconds);
            }
        }

        /// <summary>
        /// Atomically update the tick-based (0x68) heartbeat values and transmit
        /// it to the client.
        /// </summary>
        public void SendTickHeartbeat()
        {
            var aliveSince = new TimeSpan(DateTime.Now.Ticks - ConnectedSince);
            if (aliveSince.TotalSeconds < Constants.BYTE_HEARTBEAT_INTERVAL)
                return;
            var tickHeartbeat = new ServerPacket(0x68);
            // We never really want to deal with negative values
            var tickCount = Environment.TickCount & Int32.MaxValue;
            Interlocked.Exchange(ref _localTickCount, tickCount);
            tickHeartbeat.WriteInt32(tickCount);
            Enqueue(tickHeartbeat);
            Interlocked.Exchange(ref _tickHeartbeatSent, DateTime.Now.Ticks);
        }

        /// <summary>
        /// Check whether the provided byte heartbeat values match what was sent to the client.
        /// </summary>
        /// <param name="a">byteA received from client</param>
        /// <param name="b">byteB received from client</param>
        /// <returns></returns>
        public bool IsHeartbeatValid(byte a, byte b)
        {
            if (a == _heartbeatA && b == _heartbeatB)
            {
                Interlocked.Exchange(ref _byteHeartbeatReceived, DateTime.Now.Ticks);
                return true;    
            }
            return false;
        }

        /// <summary>
        /// Check whether the localTickCount for the tick heartbeat matches what was sent to the client, updating last received
        /// heartbeat ticks.
        /// </summary>
        /// <param name="localTickCount">Local (server) tick count returned from the client</param>
        /// <param name="clientTickCount">Tick count returned from the client</param>
        /// <returns>Whether or not the heartbeat is valid</returns>
        public bool IsHeartbeatValid(int localTickCount, int clientTickCount)
        {
            if (_localTickCount == localTickCount)
            {
                Interlocked.Exchange(ref _clientTickCount, clientTickCount);
                Interlocked.Exchange(ref _tickHeartbeatReceived, DateTime.Now.Ticks);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine whether either heartbeat has "expired" (meaning REAP_HEARTBEAT_INTERVAL has
        /// passed since we received a heartbeat response).
        /// </summary>
        /// <returns>True or false, indicating expiration.</returns>
        public bool IsHeartbeatExpired()
        {
            // If we have no record of sending a heartbeat, obviously it hasn't expired
            if (_tickHeartbeatSent == 0 && _byteHeartbeatSent == 0)
                return false;

            var tickSpan = new TimeSpan(_tickHeartbeatReceived - _tickHeartbeatSent);
            var byteSpan = new TimeSpan(_byteHeartbeatReceived - _byteHeartbeatSent);

            Logger.DebugFormat("cid {0}: tick heartbeat elapsed seconds {1}, byte heartbeat elapsed seconds {2}",
                ConnectionId, tickSpan.TotalSeconds, byteSpan.TotalSeconds);

            if (tickSpan.TotalSeconds > Constants.REAP_HEARTBEAT_INTERVAL ||
                byteSpan.TotalSeconds > Constants.REAP_HEARTBEAT_INTERVAL)
            {
                // DON'T FEAR THE REAPER
                Logger.InfoFormat("cid {0}: heartbeat expired");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Atomically update the last time we received a packet (in ticks).
        /// This also automatically marks the client as not idle.
        /// </summary>
        public void UpdateLastReceived(bool updateIdle = true)
        {
            Interlocked.Exchange(ref _lastReceived, DateTime.Now.Ticks);
            if (updateIdle)
                Interlocked.Exchange(ref _idle, 0);
            Logger.DebugFormat("cid {0}: lastReceived now {1}", ConnectionId, _lastReceived);
        }

        /// <summary>
        /// Atomically set whether or not a client is idle.
        /// </summary>
        public void ToggleIdle()
        {
            if (_idle == 0)
            {
                Interlocked.Exchange(ref _idle, 1);
                return;
            }
            Interlocked.Exchange(ref _idle, 0);
        }

        /// <summary>
        /// Return a boolean indicating whether or not a client is idle.
        /// </summary>
        public bool IsIdle()
        {
            return (_idle == 1);
        }

        public Client()
        {

        }

        public Client(Socket socket, Server server)
        {
            ClientState = new ClientState(socket);
            Server = server;
            Logger.InfoFormat("Connection {0} from {1}:{2}", ConnectionId,
                ((IPEndPoint)Socket.RemoteEndPoint).Address.ToString(),
                ((IPEndPoint)Socket.RemoteEndPoint).Port);
            EncryptionKey = Encoding.ASCII.GetBytes("UrkcnItnI");
            EncryptionKeyTable = new byte[1024];
            _lastReceived = DateTime.Now.Ticks;
            GlobalConnectionManifest.RegisterClient(this);
            ConnectedSince = DateTime.Now.Ticks;
            foreach( var i in Constants.PACKET_THROTTLES.Keys)
            {
                Throttle.Add(i, new ThrottleInfo(i));
            }
        }

        public void Disconnect()
        {
            GlobalConnectionManifest.DeregisterClient(this);
            World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.CleanupUser, ConnectionId));
            ClientState.Dispose();

        }

        public byte[] GenerateKey(ushort bRand, byte sRand)
        {
            var key = new byte[9];

            for (var i = 0; i < 9; ++i)
            {
                key[i] = EncryptionKeyTable[(i * (9 * i + sRand * sRand) + bRand) % 1024];
            }

            return key;
        }

        public void GenerateKeyTable(string seed)
        {
            string table = Crypto.HashString(seed, "MD5");
            table = Crypto.HashString(table, "MD5");
            for (var i = 0; i < 31; i++)
            {
                table += Crypto.HashString(table, "MD5");
            }

            EncryptionKeyTable = Encoding.ASCII.GetBytes(table);
        }

        public void Enqueue(ServerPacket packet)
        {
            Logger.DebugFormat("Enqueueing {0}", packet.Opcode);
            ClientState.SendBufferAdd(packet);
        }

        public void Redirect(Redirect redirect)
        {
            Logger.InfoFormat("Processing redirect");
            GlobalConnectionManifest.RegisterRedirect(this, redirect);
            Logger.InfoFormat("Redirect: cid {0}", this.ConnectionId);
            //GlobalConnectionManifest.DeregisterClient(this);

            redirect.Destination.ExpectedConnections.TryAdd(redirect.Id, redirect);

            var endPoint = Socket.RemoteEndPoint as IPEndPoint;

            byte[] addressBytes = IPAddress.IsLoopback(endPoint.Address) ? IPAddress.Loopback.GetAddressBytes() : Game.IpAddress.GetAddressBytes();

            Array.Reverse(addressBytes);

            var x03 = new ServerPacket(0x03);
            x03.Write(addressBytes);
            x03.WriteUInt16((ushort)redirect.Destination.Port);
            x03.WriteByte((byte)(redirect.EncryptionKey.Length + Encoding.GetEncoding(949).GetBytes(redirect.Name).Length + 7));
            x03.WriteByte(redirect.EncryptionSeed);
            x03.WriteByte((byte)redirect.EncryptionKey.Length);
            x03.Write(redirect.EncryptionKey);
            x03.WriteString8(redirect.Name);
            x03.WriteUInt32(redirect.Id);
            Thread.Sleep(100);
            Enqueue(x03);
        }

        public void LoginMessage(string message, byte type)
        {
            var x02 = new ServerPacket(0x02);
            x02.WriteByte(type);
            x02.WriteString8(message);
            Enqueue(x02);
        }

        public void SendMessage(string message, byte type)
        {
            var x0A = new ServerPacket(0x0A);
            x0A.WriteByte(type);
            x0A.WriteString16(message);
            Enqueue(x0A);
        }

    }
}
