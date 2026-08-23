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

namespace Hybrasyl.Networking;

/// <summary>
///     What a connection's first inbound byte decided its transport is. Decided exactly once,
///     in <c>Server.ReadCallback</c>, and never revisited: every retail frame starts 0xAA, so a
///     plaintext connection re-probed mid-stream would always re-decide the same way, and a TLS
///     connection's bytes stop being probeable the moment they enter the handshake.
/// </summary>
public enum TransportKind
{
    /// <summary>No data yet; the first received byte decides.</summary>
    Undecided,

    /// <summary>Retail framing on the bare socket - the only transport before HS-1621.</summary>
    Plaintext,

    /// <summary>A TLS 1.3 channel established by STARTTLS upgrade (extension dialect).</summary>
    Tls,
}
