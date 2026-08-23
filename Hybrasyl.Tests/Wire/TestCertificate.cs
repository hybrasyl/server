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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     A throwaway self-signed <c>CN=localhost</c> certificate for tests that need
///     <c>Game.TlsCertificate</c> populated.
/// </summary>
public static class TestCertificate
{
    // Generated once: the bytes are irrelevant to every assertion, and RSA keygen dominated the
    // runtime of the suites that build a loopback per test.
    private static readonly Lazy<X509Certificate2> Shared = new(CreateNew, isThreadSafe: true);

    public static X509Certificate2 Create() => Shared.Value;

    private static X509Certificate2 CreateNew()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        // Same PKCS#12 round-trip as TlsCertificateLoader: SChannel rejects the ephemeral key
        // on Windows, and this suite runs there too.
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12),
            password: null);
    }
}
