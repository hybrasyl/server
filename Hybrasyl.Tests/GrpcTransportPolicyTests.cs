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
using Hybrasyl.Networking;
using Hybrasyl.Xml.Objects;
using Xunit;

namespace Hybrasyl.Tests;

/// <summary>
///     The administrative gRPC endpoint is exposed only as mutual TLS, or by explicit request as
///     plaintext for development. Everything else refuses to bind it.
/// </summary>
/// <remarks>
///     These are the cases that used to be reachable by accident. The mode was chosen by which
///     configuration keys were present: a certificate and key with no chain produced an encrypted
///     endpoint that authenticated nobody, and omitting both produced a plaintext one. Neither
///     required an operator to ask for it, and both logged as though the server had initialized
///     normally.
/// </remarks>
public class GrpcTransportPolicyTests
{
    private static NetworkInfoSsl Config(string? cert = "grpc-cert.pem", string? key = "grpc-key.pem",
        string? chain = "grpc-chain.pem") =>
        new() { ServerCertificateFile = cert, ServerKeyFile = key, ChainCertificateFile = chain };

    [Fact]
    public void AFullyConfiguredChain_IsMutualTls()
    {
        var mode = GrpcTransportPolicy.Decide(Config(), insecureRequested: false, out var reason);

        Assert.Equal(GrpcTransportMode.MutualTls, mode);
        Assert.Contains("mutual TLS", reason);
    }

    /// <summary>
    ///     The case an operator lands on by configuring the obvious two settings. It is the shipped
    ///     example's shape, and it is the one that most looks like it worked.
    /// </summary>
    [Fact]
    public void ACertificateWithNoChain_RefusesToBind()
    {
        var mode = GrpcTransportPolicy.Decide(Config(chain: null), insecureRequested: false, out var reason);

        Assert.Equal(GrpcTransportMode.Disabled, mode);
        Assert.Contains("ChainCertificateFile", reason);
    }

    [Theory]
    [InlineData(null, "grpc-key.pem", "ServerCertificateFile")]
    [InlineData("grpc-cert.pem", null, "ServerKeyFile")]
    [InlineData("", "grpc-key.pem", "ServerCertificateFile")]
    [InlineData("grpc-cert.pem", "   ", "ServerKeyFile")]
    public void AnIncompleteCertificateConfiguration_RefusesToBind(string? cert, string? key, string named)
    {
        var mode = GrpcTransportPolicy.Decide(Config(cert, key), insecureRequested: false, out var reason);

        Assert.Equal(GrpcTransportMode.Disabled, mode);
        Assert.Contains(named, reason);
    }

    /// <summary>
    ///     An empty element is not a configured path. Deserialization does not validate against the
    ///     schema, so a present-but-blank setting reaches this code as an empty string rather than
    ///     as null.
    /// </summary>
    [Fact]
    public void ABlankChainSetting_IsNotAChain()
    {
        var mode = GrpcTransportPolicy.Decide(Config(chain: "  "), insecureRequested: false, out _);

        Assert.Equal(GrpcTransportMode.Disabled, mode);
    }

    [Fact]
    public void NoGrpcConfiguration_IsDisabledRatherThanInsecure()
    {
        var mode = GrpcTransportPolicy.Decide(null, insecureRequested: false, out var reason);

        Assert.Equal(GrpcTransportMode.Disabled, mode);
        Assert.Contains("not exposed", reason);
    }

    [Fact]
    public void InsecureIsReachableOnlyByAsking()
    {
        var mode = GrpcTransportPolicy.Decide(Config(), insecureRequested: true, out var reason);

        Assert.Equal(GrpcTransportMode.InsecureDevelopment, mode);
        Assert.Contains(GrpcTransportPolicy.InsecureEnvironmentVariable, reason);
    }

    /// <summary>
    ///     Asking for insecure with no certificates at all still yields insecure rather than
    ///     disabled: the operator asked, and a half-configured chain must not silently override
    ///     what they asked for in either direction.
    /// </summary>
    [Fact]
    public void InsecureDoesNotDependOnCertificateSettings()
    {
        var mode = GrpcTransportPolicy.Decide(Config(null, null, null), insecureRequested: true, out _);

        Assert.Equal(GrpcTransportMode.InsecureDevelopment, mode);
    }

    /// <summary>
    ///     Absent the environment variable there is no path to a plaintext administrative endpoint,
    ///     whatever the configuration says. This is the property the whole type exists for, so it
    ///     is asserted over the configuration space rather than inferred from the cases above.
    /// </summary>
    [Theory]
    [InlineData("c", "k", "ch")]
    [InlineData("c", "k", null)]
    [InlineData("c", null, null)]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    public void WithoutTheEnvironmentVariable_NoConfigurationYieldsPlaintext(string? cert, string? key,
        string? chain)
    {
        var mode = GrpcTransportPolicy.Decide(Config(cert, key, chain), insecureRequested: false, out _);

        Assert.NotEqual(GrpcTransportMode.InsecureDevelopment, mode);
    }

    [Fact]
    public void TheEnvironmentVariableIsExactlyOne()
    {
        // Guards against a truthiness check creeping in: "0", "false" and "true" must not enable it.
        foreach (var value in new[] { "0", "false", "true", "yes", "" })
        {
            Environment.SetEnvironmentVariable(GrpcTransportPolicy.InsecureEnvironmentVariable, value);
            Assert.False(GrpcTransportPolicy.InsecureRequested(), $"'{value}' should not enable insecure mode");
        }

        Environment.SetEnvironmentVariable(GrpcTransportPolicy.InsecureEnvironmentVariable, "1");
        Assert.True(GrpcTransportPolicy.InsecureRequested());

        Environment.SetEnvironmentVariable(GrpcTransportPolicy.InsecureEnvironmentVariable, null);
        Assert.False(GrpcTransportPolicy.InsecureRequested());
    }
}
