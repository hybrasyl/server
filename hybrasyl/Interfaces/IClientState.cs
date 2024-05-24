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
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System.Collections.Generic;
using System.Threading;
using Hybrasyl.Networking;

namespace Hybrasyl.Interfaces;

public interface IClientState
{
    public int BytesReceived { get; }
    public ManualResetEvent SendComplete { get; }
    public int SendBufferDepth { get; }
    public bool Connected { get; }
    public long Id { get; }
    public object ReceiveLock { get; }
    public object SendLock { get; }
    public ISocketProxy WorkSocket { get; }
    public byte[] Buffer { get; }
    public bool SendBufferEmpty { get; }
    public IEnumerable<byte> ReceiveBufferTake(int range);
    public IEnumerable<byte> ReceiveBufferPop(int range);
    public void SendBufferAdd(ServerPacket packet);
    public bool SendBufferPeek(out ServerPacket packet);
    public bool SendBufferTake(out ServerPacket packet);
    public void ResetReceive();
    public void ResetSend();
    public void Dispose();
    public bool TryGetPacket(out ClientPacket packet);
    public void ReceiveBufferAdd(ClientPacket packet);
    public bool ReceiveBufferTake(out ClientPacket packet);
}