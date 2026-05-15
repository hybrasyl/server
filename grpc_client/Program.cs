// hybctl — Hybrasyl gRPC test client.
//
// Exercises every RPC in Patron.proto and supports insecure h2c, server-auth
// TLS (system trust or pinned CA), mTLS, and a TOFU trust store for self-signed
// or otherwise untrusted certificates.

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using HybrasylGrpc;

namespace Hybrasyl.GrpcClient;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitRpc = 1;
    private const int ExitUsage = 2;
    private const int ExitTls = 3;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var opts = CliOptions.Parse(args);
            return await Dispatch(opts);
        }
        catch (UsageException ex)
        {
            if (!string.IsNullOrEmpty(ex.Message))
                Console.Error.WriteLine($"error: {ex.Message}");
            CliOptions.PrintUsage(Console.Error);
            return ExitUsage;
        }
        catch (TrustAbortException)
        {
            Console.Error.WriteLine("aborted: certificate not trusted");
            return ExitTls;
        }
        catch (RpcException ex)
        {
            Console.Error.WriteLine($"rpc failed: {ex.Status.StatusCode}: {ex.Status.Detail}");
            return ExitRpc;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.GetType().Name}: {ex.Message}");
            return ExitTls;
        }
    }

    private static async Task<int> Dispatch(CliOptions opts)
    {
        switch (opts.Command)
        {
            case "health":           return await HealthCmd(opts);
            case "users":            return await UsersCmd(opts);
            case "auth":             return await AuthCmd(opts);
            case "reset-password":   return await ResetPasswordCmd(opts);
            case "shutdown":         return await ShutdownCmd(opts);
            case "shutdown-status":  return await ShutdownStatusCmd(opts);
            case "trust":            return TrustCmd(opts);
            case "help":             CliOptions.PrintUsage(Console.Out); return ExitOk;
            default:
                throw new UsageException($"unknown command: {opts.Command}");
        }
    }

    // ---------------- RPC subcommands ----------------

    private static async Task<int> HealthCmd(CliOptions opts)
    {
        var (channel, _) = await BuildChannel(opts);
        var client = new Patron.PatronClient(channel);
        var reply = await client.HealthAsync(new Empty(), Deadline(opts));
        Console.WriteLine($"healthy: {reply.Healthy}");
        Console.WriteLine($"response: {reply.Response}");
        return reply.Healthy ? ExitOk : ExitRpc;
    }

    private static async Task<int> UsersCmd(CliOptions opts)
    {
        var (channel, _) = await BuildChannel(opts);
        var client = new Patron.PatronClient(channel);
        var reply = await client.TotalUserCountAsync(new Empty(), Deadline(opts));
        Console.WriteLine(reply.Number);
        return reply.Number < 0 ? ExitRpc : ExitOk;
    }

    private static async Task<int> AuthCmd(CliOptions opts)
    {
        if (opts.PositionalArgs.Count != 2)
            throw new UsageException("auth requires <username> <password>");
        var (channel, _) = await BuildChannel(opts);
        var client = new Patron.PatronClient(channel);
        var reply = await client.AuthAsync(
            new AuthRequest { Username = opts.PositionalArgs[0], Password = opts.PositionalArgs[1] },
            Deadline(opts));
        Console.WriteLine($"success: {reply.Success}");
        if (!string.IsNullOrEmpty(reply.Message))
            Console.WriteLine($"message: {reply.Message}");
        return reply.Success ? ExitOk : ExitRpc;
    }

    private static async Task<int> ResetPasswordCmd(CliOptions opts)
    {
        if (opts.PositionalArgs.Count != 2)
            throw new UsageException("reset-password requires <username> <new-password>");
        var (channel, _) = await BuildChannel(opts);
        var client = new Patron.PatronClient(channel);
        var reply = await client.ResetPasswordAsync(
            new ResetPasswordRequest { Username = opts.PositionalArgs[0], NewPassword = opts.PositionalArgs[1] },
            Deadline(opts));
        Console.WriteLine($"success: {reply.Success}");
        if (!string.IsNullOrEmpty(reply.Message))
            Console.WriteLine($"message: {reply.Message}");
        return reply.Success ? ExitOk : ExitRpc;
    }

    private static async Task<int> ShutdownCmd(CliOptions opts)
    {
        if (opts.PositionalArgs.Count != 1 || !int.TryParse(opts.PositionalArgs[0], out var minutes))
            throw new UsageException("shutdown requires <minutes>");
        var (channel, _) = await BuildChannel(opts);
        var client = new Patron.PatronClient(channel);
        var reply = await client.BeginShutdownAsync(
            new BeginShutdownRequest { Delay = minutes },
            Deadline(opts));
        Console.WriteLine($"success: {reply.Success}");
        Console.WriteLine($"message: {reply.Message}");
        return reply.Success ? ExitOk : ExitRpc;
    }

    private static async Task<int> ShutdownStatusCmd(CliOptions opts)
    {
        var (channel, _) = await BuildChannel(opts);
        var client = new Patron.PatronClient(channel);
        var reply = await client.IsShutdownCompleteAsync(new Empty(), Deadline(opts));
        Console.WriteLine($"complete: {reply.Success}");
        Console.WriteLine($"message: {reply.Message}");
        return ExitOk;
    }

    // ---------------- trust management ----------------

    private static int TrustCmd(CliOptions opts)
    {
        if (opts.PositionalArgs.Count == 0)
            throw new UsageException("trust requires a subcommand: list, show, remove, add");

        var sub = opts.PositionalArgs[0];
        var rest = opts.PositionalArgs.Skip(1).ToList();
        var store = TrustStore.Load(opts.TrustStorePath);

        switch (sub)
        {
            case "list":
                if (store.Entries.Count == 0)
                {
                    Console.WriteLine("(trust store is empty)");
                    return ExitOk;
                }
                foreach (var e in store.Entries)
                    Console.WriteLine($"{e.Fingerprint}  host={e.Host}  added={e.AddedAt:yyyy-MM-ddTHH:mm:ssZ}  subject={e.Cert.Subject}");
                return ExitOk;

            case "show":
                if (rest.Count != 1) throw new UsageException("trust show requires <fingerprint-prefix>");
                var matchShow = store.FindByPrefix(rest[0]);
                if (matchShow == null) { Console.Error.WriteLine($"no match for prefix: {rest[0]}"); return ExitUsage; }
                CertDisplay.WriteFull(Console.Out, matchShow.Cert, matchShow.Host, matchShow.AddedAt);
                return ExitOk;

            case "remove":
                if (rest.Count != 1) throw new UsageException("trust remove requires <fingerprint-prefix>");
                var matchRemove = store.FindByPrefix(rest[0]);
                if (matchRemove == null) { Console.Error.WriteLine($"no match for prefix: {rest[0]}"); return ExitUsage; }
                store.Entries.Remove(matchRemove);
                store.Save();
                Console.WriteLine($"removed {matchRemove.Fingerprint} (host={matchRemove.Host})");
                return ExitOk;

            case "add":
                // Re-dispatch as a Health probe with trust-add semantics.
                var probe = opts with { Command = "health", TrustAdd = true, PositionalArgs = new List<string>() };
                return HealthCmd(probe).GetAwaiter().GetResult();

            default:
                throw new UsageException($"unknown trust subcommand: {sub}");
        }
    }

    // ---------------- channel construction ----------------

    private static async Task<(GrpcChannel channel, TrustStore store)> BuildChannel(CliOptions opts)
    {
        await Task.CompletedTask;
        var store = TrustStore.Load(opts.TrustStorePath);

        var scheme = opts.UsesTls ? "https" : "http";
        var address = $"{scheme}://{opts.Host}:{opts.Port}";

        var channelOptions = new GrpcChannelOptions();

        if (opts.UsesTls)
        {
            var sslOptions = BuildSslOptions(opts, store);
            var handler = new SocketsHttpHandler
            {
                SslOptions = sslOptions,
                EnableMultipleHttp2Connections = true,
            };
            channelOptions.HttpHandler = handler;
            channelOptions.DisposeHttpClient = true;
        }
        else
        {
            // h2c requires this for Grpc.Net.Client.
            channelOptions.UnsafeUseInsecureChannelCallCredentials = false;
        }

        Console.Error.WriteLine($"connecting to {address} ({DescribeMode(opts)})");
        var channel = GrpcChannel.ForAddress(address, channelOptions);
        return (channel, store);
    }

    private static SslClientAuthenticationOptions BuildSslOptions(CliOptions opts, TrustStore store)
    {
        var clientCerts = new X509CertificateCollection();
        if (opts.ClientCertPath != null && opts.ClientKeyPath != null)
        {
            var cert = X509Certificate2.CreateFromPemFile(opts.ClientCertPath, opts.ClientKeyPath);
            // Round-trip through PFX so SslStream accepts the private key on Windows.
            var pfx = X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), password: null);
            clientCerts.Add(pfx);
        }

        X509Certificate2? caCert = null;
        if (opts.CaCertPath != null)
            caCert = X509CertificateLoader.LoadCertificateFromFile(opts.CaCertPath);

        return new SslClientAuthenticationOptions
        {
            TargetHost = opts.Host,
            ClientCertificates = clientCerts,
            RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                ValidateServerCert(cert, chain, errors, opts, store, caCert),
        };
    }

    private static bool ValidateServerCert(
        System.Security.Cryptography.X509Certificates.X509Certificate? cert,
        X509Chain? chain,
        SslPolicyErrors errors,
        CliOptions opts,
        TrustStore store,
        X509Certificate2? caCert)
    {
        if (opts.InsecureNoVerify)
        {
            Console.Error.WriteLine("warning: --insecure-no-verify in effect, NOT validating server certificate");
            return true;
        }

        if (cert is null) return false;
        var cert2 = cert as X509Certificate2 ?? new X509Certificate2(cert);

        if (errors == SslPolicyErrors.None)
            return true;

        if (caCert != null && ValidateWithCustomCa(cert2, chain, caCert))
            return true;

        if (store.IsTrusted(cert2))
            return true;

        if (opts.TrustOnce)
        {
            Console.Error.WriteLine($"trust: accepting {CertDisplay.Fingerprint(cert2)} for this session only");
            return true;
        }

        if (opts.TrustAdd)
        {
            store.Add(cert2, $"{opts.Host}:{opts.Port}");
            store.Save();
            Console.Error.WriteLine($"trust: added {CertDisplay.Fingerprint(cert2)} for {opts.Host}:{opts.Port}");
            return true;
        }

        if (opts.NoTrustPrompt || Console.IsInputRedirected)
        {
            Console.Error.WriteLine($"error: server certificate not trusted ({errors})");
            Console.Error.WriteLine($"  subject: {cert2.Subject}");
            Console.Error.WriteLine($"  sha256:  {CertDisplay.Fingerprint(cert2)}");
            Console.Error.WriteLine("hint: rerun with --trust-once, --trust-add, --cacert <path>, or interactively to prompt");
            return false;
        }

        var decision = TrustPrompt.Show(cert2, chain, errors, opts.Host, opts.Port, opts.TrustStorePath);
        switch (decision)
        {
            case TrustDecision.Permanent:
                store.Add(cert2, $"{opts.Host}:{opts.Port}");
                store.Save();
                Console.Error.WriteLine($"trust: persisted to {opts.TrustStorePath}");
                return true;
            case TrustDecision.Once:
                return true;
            case TrustDecision.Abort:
            default:
                throw new TrustAbortException();
        }
    }

    private static bool ValidateWithCustomCa(X509Certificate2 cert, X509Chain? presented, X509Certificate2 caCert)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(caCert);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (presented != null)
            foreach (var element in presented.ChainElements)
                chain.ChainPolicy.ExtraStore.Add(element.Certificate);
        return chain.Build(cert);
    }

    private static CallOptions Deadline(CliOptions opts) =>
        new CallOptions(deadline: DateTime.UtcNow.AddSeconds(opts.TimeoutSeconds));

    private static string DescribeMode(CliOptions opts)
    {
        if (!opts.UsesTls) return "insecure h2c";
        var bits = new List<string>();
        if (opts.ClientCertPath != null) bits.Add("mTLS");
        else bits.Add("TLS");
        if (opts.CaCertPath != null) bits.Add("pinned CA");
        if (opts.InsecureNoVerify) bits.Add("NO VERIFY");
        if (opts.TrustOnce) bits.Add("trust-once");
        if (opts.TrustAdd) bits.Add("trust-add");
        return string.Join(", ", bits);
    }
}

// ---------------- argument parsing ----------------

internal sealed record CliOptions
{
    public string Command { get; init; } = "help";
    public List<string> PositionalArgs { get; init; } = new();
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 2613;
    public int TimeoutSeconds { get; init; } = 10;

    public bool Tls { get; init; }
    public string? CaCertPath { get; init; }
    public string? ClientCertPath { get; init; }
    public string? ClientKeyPath { get; init; }
    public bool InsecureNoVerify { get; init; }

    public string TrustStorePath { get; init; } = TrustStore.DefaultPath();
    public bool TrustOnce { get; init; }
    public bool TrustAdd { get; init; }
    public bool NoTrustPrompt { get; init; }

    public bool UsesTls =>
        Tls || CaCertPath != null || ClientCertPath != null || InsecureNoVerify;

    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0)
            throw new UsageException("");

        string? command = null;
        var positional = new List<string>();
        var opts = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h":
                case "--help":
                    return opts with { Command = "help" };

                case "--host":
                    opts = opts with { Host = RequireValue(args, ref i, a) };
                    break;
                case "--port":
                    opts = opts with { Port = int.Parse(RequireValue(args, ref i, a)) };
                    break;
                case "--timeout":
                    opts = opts with { TimeoutSeconds = int.Parse(RequireValue(args, ref i, a)) };
                    break;

                case "--insecure":
                    // explicit no-op; insecure h2c is already the default when no TLS flag is given
                    break;
                case "--tls":
                    opts = opts with { Tls = true };
                    break;
                case "--cacert":
                    opts = opts with { CaCertPath = RequireValue(args, ref i, a), Tls = true };
                    break;
                case "--cert":
                    opts = opts with { ClientCertPath = RequireValue(args, ref i, a), Tls = true };
                    break;
                case "--key":
                    opts = opts with { ClientKeyPath = RequireValue(args, ref i, a), Tls = true };
                    break;
                case "--insecure-no-verify":
                    opts = opts with { InsecureNoVerify = true, Tls = true };
                    break;

                case "--trust-store":
                    opts = opts with { TrustStorePath = RequireValue(args, ref i, a) };
                    break;
                case "--trust-once":
                    opts = opts with { TrustOnce = true };
                    break;
                case "--trust-add":
                    opts = opts with { TrustAdd = true };
                    break;
                case "--no-trust-prompt":
                    opts = opts with { NoTrustPrompt = true };
                    break;

                default:
                    if (a.StartsWith("-"))
                        throw new UsageException($"unknown flag: {a}");
                    if (command == null) command = a;
                    else positional.Add(a);
                    break;
            }
        }

        if (command == null)
            throw new UsageException("");
        if ((opts.ClientCertPath == null) != (opts.ClientKeyPath == null))
            throw new UsageException("--cert and --key must be given together");

        return opts with { Command = command, PositionalArgs = positional };
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length) throw new UsageException($"{flag} requires a value");
        return args[++i];
    }

    public static void PrintUsage(TextWriter w)
    {
        w.WriteLine("hybctl — Hybrasyl gRPC test client");
        w.WriteLine();
        w.WriteLine("usage: dotnet run --project grpc_client -- <command> [args] [options]");
        w.WriteLine();
        w.WriteLine("commands:");
        w.WriteLine("  health                          health probe (Login/World/Lobby active?)");
        w.WriteLine("  users                           active user count");
        w.WriteLine("  auth <user> <password>          verify credentials");
        w.WriteLine("  reset-password <user> <new>     reset a user password (4-8 chars)");
        w.WriteLine("  shutdown <minutes>              schedule shutdown");
        w.WriteLine("  shutdown-status                 query shutdown progress");
        w.WriteLine("  trust list                      list trusted certs");
        w.WriteLine("  trust show <fp-prefix>          show cert details");
        w.WriteLine("  trust remove <fp-prefix>        remove a trusted cert");
        w.WriteLine("  trust add                       connect (health probe) and persist server cert");
        w.WriteLine();
        w.WriteLine("connection:");
        w.WriteLine("  --host <host>                   default: localhost");
        w.WriteLine("  --port <port>                   default: 2613");
        w.WriteLine("  --timeout <seconds>             per-RPC deadline (default 10)");
        w.WriteLine();
        w.WriteLine("transport:");
        w.WriteLine("  --insecure                      h2c (no TLS) — default");
        w.WriteLine("  --tls                           server-auth TLS using system trust");
        w.WriteLine("  --cacert <path>                 pin trust to a specific CA PEM");
        w.WriteLine("  --cert <path> --key <path>      mTLS client cert + key (PEM)");
        w.WriteLine("  --insecure-no-verify            TLS without ANY validation (debug only)");
        w.WriteLine();
        w.WriteLine("trust store (TOFU):");
        w.WriteLine($"  --trust-store <path>            default: {TrustStore.DefaultPath()}");
        w.WriteLine("  --trust-once                    accept any cert this run, no persist");
        w.WriteLine("  --trust-add                     accept and persist any cert");
        w.WriteLine("  --no-trust-prompt               fail instead of prompting");
        w.WriteLine();
        w.WriteLine("exit codes: 0 ok, 1 rpc failed, 2 usage error, 3 transport/tls error");
    }
}

internal sealed class UsageException : Exception
{
    public UsageException(string msg) : base(msg) { }
}

internal sealed class TrustAbortException : Exception { }

// ---------------- trust store ----------------

internal enum TrustDecision { Permanent, Once, Abort }

internal sealed class TrustEntry
{
    public required X509Certificate2 Cert { get; init; }
    public required string Host { get; init; }
    public required DateTime AddedAt { get; init; }
    public required string Fingerprint { get; init; }
}

internal sealed class TrustStore
{
    public string Path { get; }
    public List<TrustEntry> Entries { get; } = new();

    private TrustStore(string path) { Path = path; }

    public static string DefaultPath()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var basePath = !string.IsNullOrEmpty(xdg)
            ? xdg
            : System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config");
        return System.IO.Path.Combine(basePath, "hybrasyl", "grpc-client", "trusted.pem");
    }

    public static TrustStore Load(string path)
    {
        var store = new TrustStore(path);
        if (!File.Exists(path)) return store;

        var lines = File.ReadAllLines(path);
        string? host = null;
        DateTime? addedAt = null;
        string? fp = null;
        var pemBuf = new StringBuilder();
        var inPem = false;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (!inPem && line.StartsWith("# "))
            {
                // metadata line: "# added=... host=... fp_sha256=..."
                foreach (var part in line.Substring(2).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    var k = part.Substring(0, eq);
                    var v = part.Substring(eq + 1);
                    switch (k)
                    {
                        case "added": DateTime.TryParse(v, out var dt); addedAt = dt; break;
                        case "host": host = v; break;
                        case "fp_sha256": fp = v; break;
                    }
                }
                continue;
            }
            if (line == "-----BEGIN CERTIFICATE-----")
            {
                inPem = true;
                pemBuf.Clear();
                pemBuf.AppendLine(line);
                continue;
            }
            if (inPem)
            {
                pemBuf.AppendLine(line);
                if (line == "-----END CERTIFICATE-----")
                {
                    var cert = X509Certificate2.CreateFromPem(pemBuf.ToString());
                    var actualFp = CertDisplay.Fingerprint(cert);
                    store.Entries.Add(new TrustEntry
                    {
                        Cert = cert,
                        Host = host ?? "(unknown)",
                        AddedAt = addedAt ?? DateTime.UnixEpoch,
                        Fingerprint = fp ?? actualFp,
                    });
                    inPem = false;
                    host = null; addedAt = null; fp = null;
                }
            }
        }

        return store;
    }

    public bool IsTrusted(X509Certificate2 cert)
    {
        var fp = CertDisplay.FingerprintRaw(cert);
        return Entries.Any(e => CertDisplay.FingerprintRaw(e.Cert) == fp);
    }

    public TrustEntry? FindByPrefix(string prefix)
    {
        var norm = prefix.Replace(":", "").Replace(" ", "").ToUpperInvariant();
        var matches = Entries.Where(e => CertDisplay.FingerprintRaw(e.Cert).StartsWith(norm)).ToList();
        if (matches.Count == 0) return null;
        if (matches.Count > 1)
            throw new UsageException($"prefix matches {matches.Count} entries; provide more characters");
        return matches[0];
    }

    public void Add(X509Certificate2 cert, string host)
    {
        if (IsTrusted(cert)) return;
        Entries.Add(new TrustEntry
        {
            Cert = X509CertificateLoader.LoadCertificate(cert.GetRawCertData()),
            Host = host,
            AddedAt = DateTime.UtcNow,
            Fingerprint = CertDisplay.Fingerprint(cert),
        });
    }

    public void Save()
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("# Hybrasyl gRPC client trust store (TOFU)");
        sb.AppendLine("# Managed by `hybctl trust ...`. Edit with care.");
        sb.AppendLine();
        foreach (var e in Entries)
        {
            sb.Append("# added=").Append(e.AddedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"))
              .Append(" host=").Append(e.Host)
              .Append(" fp_sha256=").AppendLine(e.Fingerprint);
            sb.AppendLine(e.Cert.ExportCertificatePem());
            sb.AppendLine();
        }

        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, sb.ToString());
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmp,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { /* best-effort */ }
        File.Move(tmp, Path, overwrite: true);
    }
}

// ---------------- cert display ----------------

internal static class CertDisplay
{
    public static string FingerprintRaw(X509Certificate2 cert) =>
        Convert.ToHexString(SHA256.HashData(cert.GetRawCertData()));

    public static string Fingerprint(X509Certificate2 cert)
    {
        var hex = FingerprintRaw(cert);
        var sb = new StringBuilder(hex.Length + hex.Length / 2);
        for (var i = 0; i < hex.Length; i += 2)
        {
            if (i > 0) sb.Append(':');
            sb.Append(hex, i, 2);
        }
        return sb.ToString();
    }

    public static void WriteFull(TextWriter w, X509Certificate2 cert, string host, DateTime addedAt)
    {
        w.WriteLine($"Subject:     {cert.Subject}");
        w.WriteLine($"Issuer:      {cert.Issuer}");
        w.WriteLine($"Serial:      {cert.SerialNumber}");
        w.WriteLine($"Valid from:  {cert.NotBefore:u}");
        w.WriteLine($"Valid to:    {cert.NotAfter:u}");
        w.WriteLine($"SHA-256:     {Fingerprint(cert)}");
        var san = cert.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .FirstOrDefault();
        if (san != null)
        {
            var dns = string.Join(", ", san.EnumerateDnsNames());
            var ips = string.Join(", ", san.EnumerateIPAddresses().Select(x => x.ToString()));
            if (!string.IsNullOrEmpty(dns)) w.WriteLine($"SAN DNS:     {dns}");
            if (!string.IsNullOrEmpty(ips)) w.WriteLine($"SAN IP:      {ips}");
        }
        w.WriteLine($"Trusted for: {host}");
        w.WriteLine($"Added at:    {addedAt:u}");
    }
}

// ---------------- TOFU prompt ----------------

internal static class TrustPrompt
{
    public static TrustDecision Show(
        X509Certificate2 cert,
        X509Chain? chain,
        SslPolicyErrors errors,
        string host,
        int port,
        string storePath)
    {
        var w = Console.Error;
        w.WriteLine();
        w.WriteLine($"Server at {host}:{port} presented an untrusted certificate:");
        w.WriteLine($"  Subject:    {cert.Subject}");
        w.WriteLine($"  Issuer:     {cert.Issuer}{(cert.Subject == cert.Issuer ? " (self-signed)" : "")}");
        w.WriteLine($"  Valid:      {cert.NotBefore:u} → {cert.NotAfter:u}");
        var san = cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        if (san != null)
        {
            var dns = string.Join(", ", san.EnumerateDnsNames());
            var ips = string.Join(", ", san.EnumerateIPAddresses().Select(x => x.ToString()));
            if (!string.IsNullOrEmpty(dns)) w.WriteLine($"  SAN DNS:    {dns}");
            if (!string.IsNullOrEmpty(ips)) w.WriteLine($"  SAN IP:     {ips}");
        }
        w.WriteLine($"  SHA-256:    {CertDisplay.Fingerprint(cert)}");
        w.WriteLine($"  TLS errors: {errors}");
        if (chain != null && chain.ChainStatus.Length > 0)
        {
            var statuses = string.Join(", ", chain.ChainStatus.Select(s => s.Status.ToString()));
            w.WriteLine($"  Chain:      {statuses}");
        }
        w.WriteLine();
        w.WriteLine("Trust this certificate?");
        w.WriteLine($"  [t] Trust permanently (persist to {storePath})");
        w.WriteLine("  [o] Trust for this session only");
        w.WriteLine("  [a] Abort (default)");
        w.Write("Choice: ");

        var line = Console.ReadLine();
        var choice = (line ?? string.Empty).Trim().ToLowerInvariant();
        return choice switch
        {
            "t" or "trust" or "permanent" => TrustDecision.Permanent,
            "o" or "once" => TrustDecision.Once,
            _ => TrustDecision.Abort,
        };
    }
}
