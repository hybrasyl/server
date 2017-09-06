using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Objects;
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
        List<User> CurrentUsers();

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
                World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, "build"));
                return "Shutdown ControlMessage sent to Server.";
            }
            return "Shutdown ControlMessage not queued.";
        }

        public List<User> CurrentUsers() => my BadImageFormatExceptionWorld.ActiveUsers.Select(x => x.Value ).ToList();

        public User User(string name)
        {
            return World.ActiveUsers.All(x => x.Value.Name != name) ? null : World.ActiveUsers.Single(x => x.Value.Name == name).Value;
        }
    }
}
