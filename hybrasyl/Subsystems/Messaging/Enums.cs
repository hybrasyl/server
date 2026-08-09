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

// The 0x31 response type is DALib's BoardResponseType. Hybrasyl used to declare its own with the
// same eight concepts under different numbers — DisplayList/GetMailboxIndex/GetBoardIndex/
// GetMailMessage/GetBoardMessage = 1..5 against the wire's 1/4/2/5/3 — which read as a wire enum
// and was not one. Four values disagreed and four agreed, and the two builders that cast it
// straight to the wire (6/7/8) were correct only by that coincidence.