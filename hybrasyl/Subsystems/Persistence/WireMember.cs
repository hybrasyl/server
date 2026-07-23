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

using System;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     One wire member of a [Persistable] type: name, declared type, ordering, and
///     compiled accessors (expression-compiled so non-public members work at
///     near-direct-access speed; Set is null for members with no usable setter).
/// </summary>
internal sealed record WireMember(string Name, Type MemberType, int Order,
    Func<object, object?> Get, Action<object, object?>? Set);
