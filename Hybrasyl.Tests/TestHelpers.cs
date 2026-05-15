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

using System;
using System.Threading;

namespace Hybrasyl.Tests;

public static class TestHelpers
{
    /// <summary>
    /// Poll a condition until it becomes true, or timeout.
    /// Useful for waiting on async game logic (control messages, status removal, etc.)
    /// </summary>
    /// <param name="condition">The condition to wait for</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds (default 5000)</param>
    /// <param name="pollMs">How often to check in milliseconds (default 50)</param>
    /// <returns>True if the condition was met, false if timed out</returns>
    public static bool WaitFor(Func<bool> condition, int timeoutMs = 5000, int pollMs = 50)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            Thread.Sleep(pollMs);
        }
        return condition();
    }
}
