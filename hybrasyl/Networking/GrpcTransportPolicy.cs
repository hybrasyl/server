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

using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Networking;

/// <summary>How the administrative gRPC endpoint may be exposed.</summary>
public enum GrpcTransportMode
{
    /// <summary>Do not bind the endpoint.</summary>
    Disabled,

    /// <summary>TLS with a client certificate required and verified against the configured chain.</summary>
    MutualTls,

    /// <summary>Plaintext, no caller authentication. Only ever by explicit request.</summary>
    InsecureDevelopment
}

/// <summary>
///     Decides how the administrative gRPC endpoint may be exposed, given configuration.
/// </summary>
/// <remarks>
///     The endpoint has no authorization of its own: `ResetPassword` changes any account and
///     `BeginShutdown` stops the server, and the `Auth` RPC creates no session or call context.
///     Presenting a certificate this deployment's chain signed <em>is</em> the authorization model,
///     which is why the chain certificate is required rather than optional. Roles and per-RPC checks
///     are deliberately absent: the interface is administrative in its entirety and hybctl is its
///     only client. That holds only while operator certificates are the only client certificates
///     issued; if that stops being true this decision needs revisiting rather than patching.
///     <para>
///     This is a pure function separate from startup because the choice is the security-relevant
///     part and the binding is not: the mode must never fall out of which configuration keys
///     happen to be present.
///     </para>
/// </remarks>
public static class GrpcTransportPolicy
{
    /// <summary>
    ///     Set to <c>1</c> to bind the endpoint in plaintext with no caller authentication.
    /// </summary>
    /// <remarks>
    ///     An environment variable rather than a configuration element on purpose: configuration
    ///     files get copied from a development machine to a production one, and this is the single
    ///     setting where that would be worst. It has to be re-supplied in each environment that
    ///     wants it.
    /// </remarks>
    public const string InsecureEnvironmentVariable = "HYBRASYL_GRPC_INSECURE";

    /// <summary>
    ///     Whether the running environment has explicitly asked for the insecure mode.
    /// </summary>
    public static bool InsecureRequested() =>
        Environment.GetEnvironmentVariable(InsecureEnvironmentVariable) == "1";

    /// <summary>
    ///     Decide how — or whether — to expose the endpoint.
    /// </summary>
    /// <param name="config">The <c>Network/Grpc</c> element, or null if absent.</param>
    /// <param name="insecureRequested">See <see cref="InsecureRequested" />.</param>
    /// <param name="reason">Always set: why this mode was chosen, for the startup log.</param>
    public static GrpcTransportMode Decide(NetworkInfoSsl? config, bool insecureRequested, out string reason)
    {
        if (config == null)
        {
            reason = "no Network/Grpc configuration; the endpoint is not exposed";
            return GrpcTransportMode.Disabled;
        }

        // Checked before the certificate configuration, so that asking for insecure is never
        // silently overridden by a half-configured chain -- the operator gets what they asked for,
        // loudly, or nothing.
        if (insecureRequested)
        {
            reason = $"{InsecureEnvironmentVariable}=1 was set";
            return GrpcTransportMode.InsecureDevelopment;
        }

        var missing = string.Join(", ", MissingFileSettings(config));
        if (missing.Length > 0)
        {
            reason = "mutual TLS requires ServerCertificateFile, ServerKeyFile and " +
                     $"ChainCertificateFile; missing: {missing}";
            return GrpcTransportMode.Disabled;
        }

        reason = "mutual TLS; a client certificate is required and verified against the chain";
        return GrpcTransportMode.MutualTls;
    }

    private static IEnumerable<string> MissingFileSettings(NetworkInfoSsl config)
    {
        if (string.IsNullOrWhiteSpace(config.ServerCertificateFile)) yield return "ServerCertificateFile";
        if (string.IsNullOrWhiteSpace(config.ServerKeyFile)) yield return "ServerKeyFile";
        // Without this the server cannot ask for a client certificate at all, so its absence is the
        // difference between an authenticated endpoint and an encrypted anonymous one.
        if (string.IsNullOrWhiteSpace(config.ChainCertificateFile)) yield return "ChainCertificateFile";
    }
}
