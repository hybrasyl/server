using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;

namespace Hybrasyl.Networking;

public static class ClientFactory
{
    public static IClient CreateClient(ClientType type)
    {
        return type switch
        {
            ClientType.Client => new Client(),
            ClientType.TestClient => new TestClient(),
            _ => null
        };
    }
}