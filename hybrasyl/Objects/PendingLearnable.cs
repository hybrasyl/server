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

using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Objects;

/// <summary>
///     Learn-dialog continuation state: the castable on offer, the merchant offering it, and
///     which flow (skill or spell) set it. Accept handlers consume it atomically and verify
///     merchant and flow so replayed or cross-flow packets cannot re-apply side effects.
/// </summary>
public sealed record PendingLearnable(Castable Castable, uint MerchantId, bool IsSkillFlow);
