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

using System.Threading.Tasks;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Subsystems.Messaging;

namespace Hybrasyl.Objects;

public class Signpost : VisibleObject
{
    public Signpost(byte postX, byte postY, string message, bool messageboard = false,
        string boardkey = null)
    {
        X = postX;
        Y = postY;
        Message = message;
        IsMessageboard = messageboard;
        BoardKey = boardkey;
        Board = null;
        if (IsMessageboard && !string.IsNullOrEmpty(boardkey))
            Board = Game.World.WorldState.GetBoard(BoardKey);
    }

    public string Message { get; set; }
    public bool IsMessageboard { get; set; }
    public string BoardKey { get; set; }
    public Board Board { get; private set; }
    public ushort AoiEntryEffect { get; set; }
    public short AoiEntryEffectSpeed { get; set; }

    public override void OnClick(User invoker)
    {
        GameLog.DebugFormat("Signpost was clicked");
        if (!IsMessageboard)
            invoker.SendMessage(Message,
                Message.Length < 1024 ? (byte) MessageTypes.SLATE : (byte) MessageTypes.SLATE_WITH_SCROLLBAR);
        else
            invoker.Enqueue(MessagingController.GetMessageList(invoker.GuidReference, (ushort) Board.Id, 0, true)
                .Packet());
    }

    public override void AoiEntry(VisibleObject obj)
    {
        if (AoiEntryEffect != 0 && obj is User u)
            Task.Run(action: () => SendAoiEntryEffect(u));
        base.AoiEntry(obj);
    }

    public async void SendAoiEntryEffect(User u)
    {
        // TODO: improve, v hacky
        await Task.Delay(6000);
        u.SendEffect(X, Y, AoiEntryEffect, AoiEntryEffectSpeed);
    }
}