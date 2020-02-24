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
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using System.ServiceModel;

namespace Hybrasyl
{
    [ServiceContract]
    public interface IControlService
    {
        //TODO: Rework for .net core
        //[OperationContract]
        //[WebGet(UriTemplate = "/Shutdown/{key}")]
        //string Shutdown(string key);

        //[OperationContract]
        //[WebGet(UriTemplate = "/CurrentUsers")]
        //List<string> CurrentUsers();

        //[OperationContract]
        //[WebGet(UriTemplate = "/User/{name}")]
        //User User(string name);

    }

    //TODO: Rework for .net core

    //[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single, IncludeExceptionDetailInFaults = true)]
    //[AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    //public class ControlService : IControlService
    //{
    //    public string Shutdown(string key)
    //    {
    //        if (key == Constants.ShutdownPassword)
    //        {
    //            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, "build"));
    //            return "Shutdown ControlMessage sent to Server.";
    //        }
    //        return "Shutdown ControlMessage not queued.";
    //    }

    //    public List<string> CurrentUsers() => World.ActiveUsers.Select(x => x.Value.Name).ToList();

    //    public User User(string name)
    //    {
    //        return World.ActiveUsers.All(x => x.Value.Name != name) ? null : World.ActiveUsers.Single(x => x.Value.Name == name).Value;
    //    }

    //    public static string GetMotd()
    //    {
    //        //TODO: Rework
    //        //try
    //        //{
    //        //    using (HttpClient client = new HttpClient())
    //        //    {
    //        //        if (Game.Config?.ApiEndpoints?.RemoteAdminHost?.Port != 0)
    //        //            client.BaseAddress = new Uri($"{Game.Config.ApiEndpoints.RemoteAdminHost.Url}:{Game.Config.ApiEndpoints.RemoteAdminHost.Port}/api/news");
    //        //        else
    //        //            client.BaseAddress = new Uri($"{Game.Config.ApiEndpoints.RemoteAdminHost.Url}/api/news");

    //        //        var json = $"[{client.GetAsync(client.BaseAddress + "/GetMotd").Result.Content.ReadAsStringAsync().Result}]";

    //        //        dynamic motd = JArray.Parse(json);
    //        //        return motd[0].Data[0].Message;
    //        //    }
    //        //}
    //        //catch (Exception e)
    //        //{
    //        //    return $"There was an error fetching the MOTD.";
    //        //}
    //        return "Genric MOTD.";
    //    }
    //}
}
