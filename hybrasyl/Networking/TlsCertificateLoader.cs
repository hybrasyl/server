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
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Hybrasyl.Networking;

/// <summary>
///     Loads the stack-wide TLS server certificate declared by ServerConfig's
///     <c>Network/Tls</c> element. One certificate serves the whole lobby/login/world stack;
///     its presence in config is what later gates the extension-dialect capability marker.
/// </summary>
public static class TlsCertificateLoader
{
    /// <summary>
    ///     Loads the PEM certificate/key pair named by <paramref name="tls" />, with paths
    ///     resolved relative to <paramref name="dataDirectory" />. Throws rather than degrading:
    ///     a configured-but-unloadable certificate is an operator error to surface at startup,
    ///     not a reason to silently run without TLS.
    /// </summary>
    /// <exception cref="FileNotFoundException">The certificate or key file does not exist.</exception>
    public static X509Certificate2 Load(TlsInfo tls, string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(tls);
        ArgumentNullException.ThrowIfNull(dataDirectory);

        var certPath = Path.Join(dataDirectory, tls.ServerCertificateFile);
        var keyPath = Path.Join(dataDirectory, tls.ServerKeyFile);

        var certificate = X509Certificate2.CreateFromPemFile(certPath, keyPath);

        // SChannel cannot use the ephemeral private key CreateFromPemFile produces, so
        // SslStream would reject this certificate on Windows; a PKCS#12 round-trip re-binds
        // the key in a form SChannel accepts.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            certificate = X509CertificateLoader.LoadPkcs12(
                certificate.Export(X509ContentType.Pkcs12), password: null);

        return certificate;
    }
}
