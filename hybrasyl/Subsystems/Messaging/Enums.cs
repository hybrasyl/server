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

namespace Hybrasyl.Subsystems.Messaging;

public enum BoardAccessLevel
{
    Read,
    Write,       // N.B. Write implies read
    Moderate    // Moderator implies r/w access
}

public enum BoardResponseType : byte
{
    DisplayList = 0x01,
    GetMailboxIndex = 0x02,
    GetBoardIndex = 0x03,
    GetMailMessage = 0x04,
    GetBoardMessage = 0x05,
    EndResult = 0x06,
    DeleteMessage = 0x07,
    HighlightMessage = 0x08
}