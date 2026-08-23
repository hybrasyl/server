// This file is part of Project Hybrasyl.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
//
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
//
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
//
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Networking;
using Hybrasyl.Servers;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Who owns arming the next accept. <c>StartListening</c> arms one, waits for the callback to
///     release it, and arms the next; the callback must not arm one as well.
/// </summary>
[Collection("Hybrasyl")]
public class AcceptLoopWiring
{
    public AcceptLoopWiring(HybrasylFixture fixture) { }

    /// <summary>A lobby that accepts without binding a socket; <c>Active</c>'s setter is protected.</summary>
    private sealed class AcceptingLobby : Lobby
    {
        public AcceptingLobby() : base(IPAddress.Any, 1339) => Active = true;
    }

    /// <summary>
    ///     One completed accept leaves exactly one accept pending, not two.
    /// </summary>
    /// <remarks>
    ///     The callback used to call <c>BeginAccept</c> on the listener as its last act, while
    ///     <c>StartListening</c> — released by the same callback's first act — armed one too. Every
    ///     connection accepted therefore left an extra pending accept behind for the life of the
    ///     process. The count is taken on the listener socket because that is the one both armers
    ///     targeted; the accepted socket is a different object.
    /// </remarks>
    [Fact]
    public void AcceptConnection_DoesNotArmItsOwnReplacementAccept()
    {
        var lobby = new AcceptingLobby();
        var listener = new TestSocket();
        var handler = new TestSocket();

        // AcceptConnection reads the listener out of AsyncState and the accepted socket out of
        // the result itself, which is the shape TaskToAsyncResult produces.
        var accept = TaskToAsyncResult.Begin(Task.FromResult(handler), callback: null, listener);

        lobby.AcceptConnection(accept);

        var client = lobby.Clients.Values.Single();

        try
        {
            Assert.Equal(0, listener.AcceptsArmed);
        }
        finally
        {
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }
}
