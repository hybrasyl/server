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
using System.Collections.Generic;
using Hybrasyl.Internals.Attributes;
using Newtonsoft.Json;

namespace Hybrasyl.Subsystems.Players.Guilds;

[JsonObject(MemberSerialization.OptIn)]
[RedisType]
public class GuildVault : Vault
{
    public GuildVault() { }

    public GuildVault(Guid ownerGuid) : base(ownerGuid) { }

    public GuildVault(Guid ownerGuid, uint goldLimit, ushort itemLimit) : base(ownerGuid, goldLimit, itemLimit) { }

    //strings are guid identifiers
    [JsonProperty] public Guid GuildMasterGuid { get; private set; } //no restrictions

    [JsonProperty]
    public List<Guid>
        AuthorizedViewerGuids { get; private set; } //authorized to see what is stored, but cannot withdraw

    [JsonProperty]
    public List<Guid> AuthorizedWithdrawalGuids { get; private set; } //authorized to withdraw,  up to limit

    [JsonProperty] public List<Guid> CouncilMemberGuids { get; private set; } //possible restrictions?

    [JsonProperty] public int AuthorizedWithdrawalLimit { get; private set; }

    [JsonProperty] public int CouncilMemberLimit { get; private set; }
}