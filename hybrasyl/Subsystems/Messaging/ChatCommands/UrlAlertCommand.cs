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

using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

/// <summary>
///     TEST / PROBE — emit an S→C 0x66 in its UrlAlert form (subtype 1 or 2) to see how the client's
///     UrlAlertPane renders it. The homepage/account form is subtype 3 (a single string8 URL); subtypes
///     1/2 carry two string16s and pop an alert with text + a clickable URL. See
///     <c>User.ShowUrlAlertProbe</c> and <c>samhail/binary/0x30-npcdialog-recv</c>.
/// </summary>
internal class UrlAlertCommand : ChatCommand
{
    public new static string Command = "urlalert";
    public new static string ArgumentText = "<1|2>";

    public new static string HelpText =
        "Emit a raw 0x66 UrlAlert (subtype 1 or 2) to probe the client's UrlAlertPane rendering.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var subtype = (byte)1;
        if (args.Length >= 1 && byte.TryParse(args[0], out var parsed))
            subtype = parsed;

        user.ShowUrlAlertProbe(subtype);
        return Success($"Sent 0x66 UrlAlert probe, subtype {subtype}.");
    }
}
