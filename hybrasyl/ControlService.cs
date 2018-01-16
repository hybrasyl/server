using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Objects;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

namespace Hybrasyl
{
    [ServiceContract]
    public interface IControlService
    {
        [OperationContract]
        [WebGet(UriTemplate = "/Shutdown/{key}")]
        string Shutdown(string key);

        [OperationContract]
        [WebGet(UriTemplate = "/CurrentUsers")]
        List<string> CurrentUsers();

        [OperationContract]
        [WebGet(UriTemplate = "/User/{name}")]
        User User(string name);

    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single, IncludeExceptionDetailInFaults = true)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class ControlService : IControlService
    {
        public string Shutdown(string key)
        {
            if (key == Constants.ShutdownPassword)
            {
                World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, "build"));
                return "Shutdown ControlMessage sent to Server.";
            }
            return "Shutdown ControlMessage not queued.";
        }

        public List<string> CurrentUsers() => World.ActiveUsers.Select(x => x.Value.Name).ToList();

        public User User(string name)
        {
            return World.ActiveUsers.All(x => x.Value.Name != name) ? null : World.ActiveUsers.Single(x => x.Value.Name == name).Value;
        }

        public static string GetMotd()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri($"http://{Game.Config.ApiEndpoints.RemoteAdminHost.BindAddress}/api/news");

                    var json = $"[{client.GetAsync(client.BaseAddress + "/GetMotd").Result.Content.ReadAsStringAsync().Result}]";

                    dynamic motd = JArray.Parse(json);
                    return motd[0].Data[0].Message;
                }
            }
            catch (Exception)
            {
                return "There was an error fetching the MOTD.";
            }
        }
    }
}
