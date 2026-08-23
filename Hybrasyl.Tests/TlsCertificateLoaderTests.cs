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

using Hybrasyl.Networking;
using Hybrasyl.Xml.Objects;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Hybrasyl.Tests;

public class TlsCertificateLoaderTests : IDisposable
{
    private readonly DirectoryInfo _dataDirectory = Directory.CreateTempSubdirectory("hybtls");

    public void Dispose() => _dataDirectory.Delete(recursive: true);

    private TlsInfo WritePemPair(string certFile = "server.crt", string keyFile = "server.key")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=hybrasyl-test", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var certPath = Path.Join(_dataDirectory.FullName, certFile);
        var keyPath = Path.Join(_dataDirectory.FullName, keyFile);
        Directory.CreateDirectory(Path.GetDirectoryName(certPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        File.WriteAllText(certPath, certificate.ExportCertificatePem());
        File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());

        return new TlsInfo { ServerCertificateFile = certFile, ServerKeyFile = keyFile };
    }

    [Fact]
    public void Load_ReturnsCertificateWithPrivateKey_WhenPemPairIsValid()
    {
        var tls = WritePemPair();

        using var certificate = TlsCertificateLoader.Load(tls, _dataDirectory.FullName);

        Assert.True(certificate.HasPrivateKey);
        Assert.Equal("CN=hybrasyl-test", certificate.Subject);
    }

    [Fact]
    public void Load_ResolvesFilesRelativeToDataDirectory()
    {
        var tls = WritePemPair(certFile: Path.Join("certs", "server.crt"),
            keyFile: Path.Join("certs", "server.key"));

        using var certificate = TlsCertificateLoader.Load(tls, _dataDirectory.FullName);

        Assert.True(certificate.HasPrivateKey);
    }

    [Fact]
    public void Load_Throws_WhenCertificateFileIsMissing()
    {
        var tls = WritePemPair();
        tls.ServerCertificateFile = "nonexistent.crt";

        var e = Assert.Throws<FileNotFoundException>(
            () => TlsCertificateLoader.Load(tls, _dataDirectory.FullName));
        Assert.Contains("nonexistent.crt", e.FileName);
    }

    [Fact]
    public void Load_Throws_WhenKeyFileIsMissing()
    {
        var tls = WritePemPair();
        tls.ServerKeyFile = "nonexistent.key";

        var e = Assert.Throws<FileNotFoundException>(
            () => TlsCertificateLoader.Load(tls, _dataDirectory.FullName));
        Assert.Contains("nonexistent.key", e.FileName);
    }
}
