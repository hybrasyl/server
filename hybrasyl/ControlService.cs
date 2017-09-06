using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Hybrasyl
{
    [ServiceContract]
    public interface IControlService
    {
        [OperationContract]
        [WebGet(UriTemplate = "/Shutdown/{key}")]
        string Shutdown(string key);

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
    }
}
