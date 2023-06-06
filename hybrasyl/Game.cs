/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using Grpc.Core;
using Hybrasyl.Utility;
using Hybrasyl.Xml.Objects;
using HybrasylGrpc;
using Newtonsoft.Json.Linq;
using Sentry;
using Serilog;
using Serilog.Core;
using System.CommandLine;
using Hybrasyl.Xml.Manager;

namespace Hybrasyl;

public static class Game
{
    public static readonly object SyncObj = new();
    public static IPAddress IpAddress;
    public static IPAddress RedirectTarget;

    public static ManualResetEvent allDone = new(false);
    private static long Active;

    private static Monolith _monolith;
    private static MonolithControl _monolithControl;

    private static Thread _lobbyThread;
    private static Thread _loginThread;
    private static Thread _worldThread;
    private static Thread _spawnThread;
    private static Thread _controlThread;

    private static readonly Dictionary<Guid, Server> Servers = new();

    private static Grpc.Core.Server GrpcServer;

    public static LoggingLevelSwitch LevelSwitch;

    private static readonly CancellationTokenSource CancellationTokenSource = new();

    public static int ShutdownTimeRemaining = -1;
    public static bool ShutdownComplete;

    public static readonly Dictionary<ushort, ushort> ClosedDoorSprites = new()
    {
        { 1994, 1997 }, { 2000, 2003 }, { 2163, 2164 }, { 2165, 2196 }, { 2197, 2198 }, { 2227, 2228 },
        { 2229, 2260 }, { 2261, 2262 }, { 2291, 2292 }, { 2293, 2328 }, { 2329, 2330 }, { 2432, 2436 },
        { 2461, 2465 }, { 2673, 2674 }, { 2675, 2680 }, { 2681, 2682 }, { 2687, 2688 }, { 2689, 2694 },
        { 2695, 2696 }, { 2714, 2715 }, { 2721, 2722 }, { 2727, 2728 }, { 2734, 2735 }, { 2761, 2762 },
        { 2768, 2769 }, { 2776, 2777 }, { 2783, 2784 }, { 2850, 2851 }, { 2852, 2857 }, { 2858, 2859 },
        { 2874, 2875 }, { 2876, 2881 }, { 2882, 2883 }, { 2897, 2898 }, { 2903, 2904 }, { 2923, 2924 },
        { 2929, 2930 }, { 2945, 2946 }, { 2951, 2952 }, { 2971, 2972 }, { 2977, 2978 }, { 2993, 2994 },
        { 2999, 3000 }, { 3019, 3020 }, { 3025, 3026 }, { 3058, 3059 }, { 3066, 3067 }, { 3090, 3091 },
        { 3098, 3099 }, { 3118, 3119 }, { 3126, 3127 }, { 3150, 3151 }, { 3158, 3159 }, { 3178, 3179 },
        { 3186, 3187 }, { 3210, 3211 }, { 3218, 3219 }, { 4519, 4520 }, { 4521, 4523 }, { 4524, 4525 },
        { 4527, 4528 }, { 4529, 4532 }, { 4533, 4534 }, { 4536, 4537 }, { 4538, 4540 }, { 4541, 4542 }
    };

    public static readonly Dictionary<ushort, ushort> OpenDoorSprites = new()
    {
        { 1997, 1994 }, { 2003, 2000 }, { 2164, 2163 }, { 2196, 2165 }, { 2198, 2197 }, { 2228, 2227 },
        { 2260, 2229 }, { 2262, 2261 }, { 2292, 2291 }, { 2328, 2293 }, { 2330, 2329 }, { 2436, 2432 },
        { 2465, 2461 }, { 2674, 2673 }, { 2680, 2675 }, { 2682, 2681 }, { 2688, 2687 }, { 2694, 2689 },
        { 2696, 2695 }, { 2715, 2714 }, { 2722, 2721 }, { 2728, 2727 }, { 2735, 2734 }, { 2762, 2761 },
        { 2769, 2768 }, { 2777, 2776 }, { 2784, 2783 }, { 2851, 2850 }, { 2857, 2852 }, { 2859, 2858 },
        { 2875, 2874 }, { 2881, 2876 }, { 2883, 2882 }, { 2898, 2897 }, { 2904, 2903 }, { 2924, 2923 },
        { 2930, 2929 }, { 2946, 2945 }, { 2952, 2951 }, { 2972, 2971 }, { 2978, 2977 }, { 2994, 2993 },
        { 3000, 2999 }, { 3020, 3019 }, { 3026, 3025 }, { 3059, 3058 }, { 3067, 3066 }, { 3091, 3090 },
        { 3099, 3098 }, { 3119, 3118 }, { 3127, 3126 }, { 3151, 3150 }, { 3159, 3158 }, { 3179, 3178 },
        { 3187, 3186 }, { 3211, 3210 }, { 3219, 3218 }, { 4520, 4519 }, { 4523, 4521 }, { 4525, 4524 },
        { 4528, 4527 }, { 4532, 4529 }, { 4534, 4533 }, { 4537, 4536 }, { 4540, 4538 }, { 4542, 4541 }
    };

    public static readonly Dictionary<ushort, bool> DoorSprites = new()
    {
        { 1994, true }, { 1997, true }, { 2000, true }, { 2003, true }, { 2163, true },
        { 2164, true }, { 2165, true }, { 2196, true }, { 2197, true }, { 2198, true },
        { 2227, true }, { 2228, true }, { 2229, true }, { 2260, true }, { 2261, true },
        { 2262, true }, { 2291, true }, { 2292, true }, { 2293, true }, { 2328, true },
        { 2329, true }, { 2330, true }, { 2432, true }, { 2436, true }, { 2461, true },
        { 2465, true }, { 2673, true }, { 2674, true }, { 2675, true }, { 2680, true },
        { 2681, true }, { 2682, true }, { 2687, true }, { 2688, true }, { 2689, true },
        { 2694, true }, { 2695, true }, { 2696, true }, { 2714, true }, { 2715, true },
        { 2721, true }, { 2722, true }, { 2727, true }, { 2728, true }, { 2734, true },
        { 2735, true }, { 2761, true }, { 2762, true }, { 2768, true }, { 2769, true },
        { 2776, true }, { 2777, true }, { 2783, true }, { 2784, true }, { 2850, true },
        { 2851, true }, { 2852, true }, { 2857, true }, { 2858, true }, { 2859, true },
        { 2874, true }, { 2875, true }, { 2876, true }, { 2881, true }, { 2882, true },
        { 2883, true }, { 2897, true }, { 2898, true }, { 2903, true }, { 2904, true },
        { 2923, true }, { 2924, true }, { 2929, true }, { 2930, true }, { 2945, true },
        { 2946, true }, { 2951, true }, { 2952, true }, { 2971, true }, { 2972, true },
        { 2977, true }, { 2978, true }, { 2993, true }, { 2994, true }, { 2999, true },
        { 3000, true }, { 3019, true }, { 3020, true }, { 3025, true }, { 3026, true },
        { 3058, true }, { 3059, true }, { 3066, true }, { 3067, true }, { 3090, true },
        { 3091, true }, { 3098, true }, { 3099, true }, { 3118, true }, { 3119, true },
        { 3126, true }, { 3127, true }, { 3150, true }, { 3151, true }, { 3158, true },
        { 3159, true }, { 3178, true }, { 3179, true }, { 3186, true }, { 3187, true },
        { 3210, true }, { 3211, true }, { 3218, true }, { 3219, true }, { 4519, true },
        { 4520, true }, { 4521, true }, { 4523, true }, { 4524, true }, { 4525, true },
        { 4527, true }, { 4528, true }, { 4529, true }, { 4532, true }, { 4533, true },
        { 4534, true }, { 4536, true }, { 4537, true }, { 4538, true }, { 4540, true },
        { 4541, true }, { 4542, true }
    };

    public static Lobby Lobby { get; set; }
    public static Login Login { get; set; }
    public static World World { get; set; }
    public static byte[] ServerTable { get; private set; }
    public static uint ServerTableCrc { get; private set; }
    public static byte[] Notification { get; set; }
    public static uint NotificationCrc { get; set; }
    public static byte[] Collisions { get; set; }

    public static AssemblyInfo Assemblyinfo { get; set; }

    public static DateTime StartDate { get; set; }
    public static string CommitLog { get; private set; }

    public static IDisposable Sentry { get; private set; }
    public static bool SentryEnabled { get; private set; }

    public static IMetricsRoot MetricsStore { get; private set; }

    public static ServerConfig ActiveConfiguration { get; set; }
    public static string WorldDataDirectory { get; set; }
    public static string DataDirectory { get; set; }
    public static string LogDirectory { get; set; }
    public static string ActiveConfigurationName { get; set; }

    public static T GetServerByGuid<T>(Guid g) where T : Server => Servers.ContainsKey(g) ? (T) Servers[g] : null;

    public static T GetDefaultServer<T>() where T : Server
    {
        return Servers.Values.FirstOrDefault(predicate: x => x is T && x.Default) as T;
    }

    public static Guid GetDefaultServerGuid<T>() where T : Server
    {
        return Servers.FirstOrDefault(predicate: x => x.Value is T && x.Value.Default).Value?.Guid ?? Guid.Empty;
    }

    public static void RegisterServer(Server s)
    {
        Servers[s.Guid] = s;
    }


    public static void ToggleActive()
    {
        if (Interlocked.Read(ref Active) == 0)
        {
            Interlocked.Exchange(ref Active, 1);
            return;
        }

        Interlocked.Exchange(ref Active, 0);
    }

    public static bool IsActive()
    {
        if (Interlocked.Read(ref Active) == 0)
            return false;
        return true;
    }

    public static void ReportException(Exception e)
    {
        if (SentryEnabled)
            Task.Run(function: () => SentrySdk.CaptureException(e));
    }

    public static void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
        Shutdown();
    }

    public static void Shutdown()
    {
        Log.Warning("Hybrasyl: all servers shutting down");

        // Server is shutting down. For Lobby and Login, this terminates the TCP listeners;
        // for World, this triggers a logoff for all logged in users and then terminates. After
        // termination, the queue consumer is stopped as well.
        // For a true restart we'll need to do a few other things; stop timers, etc.

        CancellationTokenSource.Cancel();
        Lobby?.Shutdown();
        Login?.Shutdown();
        World?.Shutdown();
        // Stop consumers, which will also empty queues
        World?.StopQueueConsumer();
        World?.StopControlConsumers();

        Thread.Sleep(2000);
        Log.Warning("Hybrasyl {Version}: shutdown complete.", Assemblyinfo.Version);
        ShutdownComplete = true;
        //host.Close();
    }

    // <summary>Hybrasyl, a DOOMVAS-compatible MMO server</summary>
    // <param name="worldDir">The world data directory to use. Defaults to ~/Hybrasyl on Linux or My Documents\Hybrasyl on Windows.</param>
    // <param name="logDir">The directory to use to write logs. Defaults to ~/Hybrasyl/logs on Linux or My Documents\Hybrasyl\logs on Windows.</param>
    public static void Main(string[] args)
    {
        var dataOption = new Option<string>(name: "--datadir",
            description: "The data directory to be used for the server. Defaults to ~\\Hybrasyl\\world");

        var worldDataOption = new Option<string>(name: "--worlddatadir",
            description: "The XML data directory to be used for the server. Defaults to DATADIR\\xml");

        var logdirOption = new Option<string>(name: "--logdir",
            description: "The directory for log output from the server. Defaults to DATADIR\\logs");

        var configOption = new Option<string>(name: "--config",
            description: "The named configuration to use in the world directory. Defaults to default");

        var rootCommand = new RootCommand("Hybrasyl, a DOOMVAS-compatible MMO server");

        rootCommand.AddOption(dataOption);
        rootCommand.AddOption(worldDataOption);
        rootCommand.AddOption(logdirOption);
        rootCommand.AddOption(configOption);

        rootCommand.SetHandler(StartServer,
        dataOption, worldDataOption, logdirOption, configOption);

        rootCommand.Invoke(args);
    }

    public static void StartServer(string dataDir=null, string worldDir=null, string logDir=null, string configName=null)
    {
        Assemblyinfo = new AssemblyInfo(Assembly.GetEntryAssembly());
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        
        // Gather our directories from env vars / command line switches

        var data = Environment.GetEnvironmentVariable("DATA_DIR") ?? dataDir;
        var world = Environment.GetEnvironmentVariable("WORLD_DIR") ?? worldDir;
        var log = Environment.GetEnvironmentVariable("LOG_DIR") ?? logDir;
        var config = Environment.GetEnvironmentVariable("CONFIG") ?? configName;
        
        DataDirectory = string.IsNullOrWhiteSpace(data) ? 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Hybrasyl", "world") : data;
        WorldDataDirectory = string.IsNullOrWhiteSpace(world) ? 
            Path.Combine(DataDirectory, "xml") : world;
        LogDirectory = string.IsNullOrWhiteSpace(log) ? 
            Path.Combine(DataDirectory, "logs") : log;
        ActiveConfigurationName = string.IsNullOrWhiteSpace(config) ? "default" : config;
        
        // Set our exit handler
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        Log.Information($"Data directory: {DataDirectory}");
        Log.Information($"World directory: {WorldDataDirectory}");
        Log.Information($"Log directory: {LogDirectory}");

        if (!Directory.Exists(LogDirectory))
        {
            Log.Fatal($"The specified log directory {LogDirectory} does not exist or cannot be accessed.");
            Log.Fatal(
                "Hybrasyl cannot start without a writable log directory, so it will automatically close in 10 seconds.");
            Thread.Sleep(10000);
            return;
        }
        var manager = new XmlDataManager(WorldDataDirectory);

        try
        {
            manager.LoadData();
            manager.LogResult(Log.Logger);
        }
        catch (FileNotFoundException ex)
        {
            Log.Fatal($"An XML directory (world data) was not found at {manager.RootPath}.");
            Log.Fatal("This may be the first time you've run the server. If so, please take a look");
            Log.Fatal("at the server documentation at github.com/hybrasyl/server.");
            Log.Fatal("We also recommend you look at the example config.xml in the community database");
            Log.Fatal("which can be found at github.com/hybrasyl/ceridwen .");
            Log.Fatal("A data directory must exist for Hybrasyl to continue loading.");
            Log.Fatal(
                "Hybrasyl cannot start without a readable world data directory, so it will automatically close in 10 seconds.");
            Thread.Sleep(10000);
            return;

        }
        catch (Exception ex)
        {
            Log.Fatal($"World data could not be loaded: {ex}");
            Log.Fatal(
                "Hybrasyl cannot start without a working world data directory, so it will automatically close in 10 seconds.");
            return;
        }

        if (manager.Count<ServerConfig>() == 0)
        {
            var loadResult = manager.GetLoadResult<ServerConfig>();

            Log.Fatal("A server configuration file was not found or could not be loaded.");
            Log.Fatal("Please take a look at the server documentation at github.com/hybrasyl/server.");
            Log.Fatal("We also recommend you look at the example config.xml in the community database");
            Log.Fatal("which can be found at github.com/hybrasyl/ceridwen .");
            Log.Fatal(
                $"We are currently looking in:\n{manager.RootPath}{Path.DirectorySeparatorChar}serverconfigs for a config file.");
            if (loadResult.ErrorCount > 0)
                Log.Fatal("Errors were encountered processing server configuration:");
            foreach (var error in loadResult.Errors)
            {
                Log.Fatal($"{error.Key}: {error.Value}");
            }

            Log.Fatal(
                "Hybrasyl cannot start without a server configuration file, so it will automatically close in 10 seconds.");
            Thread.Sleep(10000);
            return;
        }

        if (!manager.TryGetValue(ActiveConfigurationName, out ServerConfig activeConfiguration))
        {
            Log.Fatal(
                $"You specified a server configuration name of {ActiveConfigurationName}, but there are no configurations with that name.");
            Log.Fatal(
                $"Active configurations that were found in {manager.RootPath}{Path.DirectorySeparatorChar}serverconfigs: {string.Join(" ", manager.Values<ServerConfig>().Select(x => x.Name))}");
            Log.Fatal(
                "Hybrasyl cannot start without a server configuration, so it will automatically close in 10 seconds.");
            Thread.Sleep(10000);
            return;
        }

        // Sanity check: ensure our localization exists
        // TODO: alpha9
        if (!manager.TryGetValue(activeConfiguration.Locale, out Localization locale))
        {
            Log.Fatal(
                $"You specified a locale of en_us, but there are no locales with that name.");
            Log.Fatal(
                $"Make sure a localization configuration exists in {manager.RootPath}\\localizations and that it matches what is defined in the server configuration.");
            Log.Fatal(
                $"Active configurations that were found in {manager.RootPath}{Path.DirectorySeparatorChar}localizations: {string.Join(" ", manager.Values<Localization>().Select(x => x.Locale))}");
            Log.Fatal(
                "Hybrasyl cannot start without a properly set locale, so it will automatically close in 10 seconds.");
            Thread.Sleep(10000);

            return;
        }

        Log.Information($"Configuration file: {activeConfiguration.Filename} ({activeConfiguration.Name}) loaded");
        activeConfiguration.InitializeClientSettings();
        activeConfiguration.Constants ??= new ServerConstants();
        // Set our active configuration to the one we just loaded
        ActiveConfiguration = activeConfiguration;
        
        // Configure logging 
        GameLog.Initialize(LogDirectory, activeConfiguration.Logging);

        // We don't want any of NCalc's garbage 
        Trace.Listeners.RemoveAt(0);

        Log.Information("Hybrasyl: server start");
        Log.Information("Welcome to Project Hybrasyl: this is Hybrasyl server {0}\n\n", Assemblyinfo.Version);
        
        Log.Information($"Hybrasyl {Assemblyinfo.Version} (commit {Assemblyinfo.GitHash}) starting.");
        Log.Information("{Copyright} - this program is licensed under the GNU AGPL, version 3.",
            Assemblyinfo.Copyright);

        // Set up metrics collection
        // TODO: make configurable
        var env = Environment.GetEnvironmentVariable("HYB_ENV");

        var builder = new MetricsBuilder().Configuration.Configure(
            setupAction: options =>
            {
                options.DefaultContextLabel = "Hybrasyl";
                options.GlobalTags.Add("Environment", env ?? "dev");
                options.Enabled = true;
                options.ReportingEnabled = true;
            });

        if (activeConfiguration.ApiEndpoints?.MetricsEndpoint != null)
            MetricsStore = builder.Report.ToHostedMetrics(
                setupAction: io =>
                {
                    io.HostedMetrics.BaseUri = new Uri(activeConfiguration.ApiEndpoints.MetricsEndpoint.Url);
                    io.HostedMetrics.ApiKey = activeConfiguration.ApiEndpoints.MetricsEndpoint.ApiKey;
                    io.HttpPolicy.BackoffPeriod = TimeSpan.FromSeconds(15);
                    io.HttpPolicy.FailuresBeforeBackoff = 5;
                    io.HttpPolicy.Timeout = TimeSpan.FromSeconds(10);
                    io.FlushInterval = TimeSpan.FromSeconds(20);
                }).Build();
        else
            MetricsStore = builder.Build();

        try
        {
            if (!string.IsNullOrEmpty(activeConfiguration.ApiEndpoints?.Sentry?.Url ?? null))
            {
                Sentry = SentrySdk.Init(configureOptions: i =>
                    {
                        i.Dsn = activeConfiguration.ApiEndpoints.Sentry.Url;
                        i.Environment = env ?? "dev";
                    }
                );
                SentryEnabled = true;
                Log.Information("Sentry: exception reporting enabled");
            }
            else
            {
                Log.Information("Sentry: exception reporting disabled");
                SentryEnabled = false;
            }
        }
        catch (Exception e)
        {
            Log.Warning("Sentry: exception reporting disabled, unknown error: {e}", e);
            SentryEnabled = false;
        }

        LoadCollisions();

        // For right now we don't support binding to different addresses; the support in the XML
        // is for a distant future where that may be desirable.
        if (activeConfiguration.Network.Login.ExternalAddress != null)
            // We can have a hostname here to support ease of running in Docker; try to naively resolve it
            RedirectTarget = Dns.GetHostAddresses(activeConfiguration.Network.Lobby.ExternalAddress).FirstOrDefault();

        IpAddress = IPAddress.Parse(activeConfiguration.Network.Lobby.BindAddress);

        Lobby = new Lobby(activeConfiguration.Network.Lobby.Port, true);
        Login = new Login(activeConfiguration.Network.Login.Port, true);
        // TODO: alpha9
        World = new World(activeConfiguration.Network.World.Port, activeConfiguration.DataStore,
            manager, activeConfiguration.Locale, true);

        Lobby.StopToken = CancellationTokenSource.Token;
        Login.StopToken = CancellationTokenSource.Token;
        World.StopToken = CancellationTokenSource.Token;

        _monolith = new Monolith();
        _monolithControl = new MonolithControl();

        if (!World.InitWorld())
        {
            Log.Fatal(
                "Hybrasyl cannot continue loading. A fatal error occurred while initializing the world. Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(1);
        }

        byte[] addressBytes;
        addressBytes = IpAddress.GetAddressBytes();
        Array.Reverse(addressBytes);

        using (var multiServerTableStream = new MemoryStream())
        {
            using (var multiServerTableWriter = new BinaryWriter(multiServerTableStream, Encoding.ASCII, true))
            {
                multiServerTableWriter.Write((byte) 1);
                multiServerTableWriter.Write((byte) 1);
                multiServerTableWriter.Write(addressBytes);
                multiServerTableWriter.Write((byte) (2611 / 256));
                multiServerTableWriter.Write((byte) (2611 % 256));
                multiServerTableWriter.Write(Encoding.ASCII.GetBytes("Hybrasyl;Hybrasyl Production\0"));
            }

            ServerTableCrc = ~Crc32.Calculate(multiServerTableStream.ToArray());

            using (var compressedMultiServerTableStream = new MemoryStream())
            {
                ZlibCompression.Compress(multiServerTableStream, compressedMultiServerTableStream);
                ServerTable = compressedMultiServerTableStream.ToArray();
            }
        }

        using (var stipulationStream = new MemoryStream())
        {
            using (var stipulationWriter = new StreamWriter(stipulationStream, Encoding.ASCII, 1024, true))
            {
                var serverMsgFileName = Path.Combine(DataDirectory, "server.msg");

                if (File.Exists(serverMsgFileName))
                {
                    stipulationWriter.Write(File.ReadAllText(serverMsgFileName));
                }
                else
                {
                    if (string.IsNullOrEmpty(activeConfiguration.Motd))
                        stipulationWriter.Write(
                            $"Welcome to Hybrasyl!\n\nThis is Hybrasyl (version {Assemblyinfo.Version}, commit {Assemblyinfo.GitHash}).\n\nFor more information please visit http://www.hybrasyl.com");
                    else
                        stipulationWriter.Write(activeConfiguration.Motd);
                }

                stipulationWriter.Write("\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n");
            }

            NotificationCrc = ~Crc32.Calculate(stipulationStream.ToArray());

            using (var compressedStipulationStream = new MemoryStream())
            {
                ZlibCompression.Compress(stipulationStream, compressedStipulationStream);
                Notification = compressedStipulationStream.ToArray();
            }
        }

        World.StartTimers();
        World.StartQueueConsumer();
        World.StartControlConsumers();

        ToggleActive();
        StartDate = DateTime.Now;

        _lobbyThread = new Thread(Lobby.StartListening);
        _loginThread = new Thread(Login.StartListening);
        _worldThread = new Thread(World.StartListening);

        _spawnThread = new Thread(_monolith.Start);
        _controlThread = new Thread(_monolithControl.Start);

        _lobbyThread.Start();
        _loginThread.Start();
        _worldThread.Start();
        _spawnThread.Start();
        _controlThread.Start();

        Task.Run(CheckVersion).GetAwaiter();
        Task.Run(GetCommitLog).GetAwaiter();

        GrpcServer = null;

        // Uncomment for GRPC troubleshooting
        // Environment.SetEnvironmentVariable("GRPC_VERBOSITY", "debug");

        // Start GRPC server
        if (activeConfiguration.Network.Grpc != null)
        {
            var ssl_enabled = activeConfiguration.Network.Grpc.ServerCertificateFile != null &&
                              activeConfiguration.Network.Grpc.ServerKeyFile != null;

            if (ssl_enabled)
            {
                var certPath = Path.Join(DataDirectory, "ssl", activeConfiguration.Network.Grpc.ServerCertificateFile);
                var keyPath = Path.Join(DataDirectory, "ssl", activeConfiguration.Network.Grpc.ServerKeyFile);

                SslServerCredentials credentials;
                // Load credentials
                try
                {
                    var cert = File.ReadAllText(certPath);
                    var key = File.ReadAllText(keyPath);
                    var keypair_list = new List<KeyCertificatePair> { new(cert, key) };
                    if (activeConfiguration.Network.Grpc.ChainCertificateFile != null)
                    {
                        var chaincerts = File.ReadAllText(Path.Join(DataDirectory, "ssl",
                            activeConfiguration.Network.Grpc.ChainCertificateFile));
                        credentials = new SslServerCredentials(keypair_list, chaincerts,
                            SslClientCertificateRequestType.RequestAndRequireAndVerify);
                    }
                    else
                    {
                        // Note, without a chain certificate, only the connection is secure; there is no authentication
                        credentials = new SslServerCredentials(keypair_list);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("GRPC: server initialization: key/cert load failed: {e}", e);
                    Log.Error("GRPC: server disabled");
                    credentials = null;
                }

                if (credentials != null)
                {
                    GrpcServer = new Grpc.Core.Server
                    {
                        Services = { Patron.BindService(new PatronServer()) },
                        Ports =
                        {
                            new ServerPort(activeConfiguration.Network.Grpc.BindAddress,
                                activeConfiguration.Network.Grpc.Port, credentials)
                        }
                    };
                    Log.Information("GRPC: SSL server initialized");
                }
            }
            else
            {
                // Insecure mode, should be used for development only
                GrpcServer = new Grpc.Core.Server
                {
                    Services = { Patron.BindService(new PatronServer()) },
                    Ports =
                    {
                        new ServerPort(activeConfiguration.Network.Grpc.BindAddress, activeConfiguration.Network.Grpc.Port,
                            ServerCredentials.Insecure)
                    }
                };
                Log.Information("GRPC: server initialized (insecure, use for development only)");
            }
        }

        if (GrpcServer != null)
            try
            {
                GrpcServer.Start();
            }
            catch (IOException e)
            {
                GameLog.Info("GRPC: server start failed: {e}", e);
            }

        while (true)
        {
            if (!IsActive())
            {
                CancellationTokenSource.Cancel();
                break;
            }

            Thread.Sleep(5);
        }

        Shutdown();
        GrpcServer.ShutdownAsync().Wait();
    }

    private static async void CheckVersion()
    {
        if (Assemblyinfo.GitHash == "unknown")
        {
            GameLog.Error("Server update check skipped, git hash not found in assemblyinfo.");
            return;
        }

        try
        {
            using var client = new HttpClient();
            using var res = await client.GetAsync("https://www.hybrasyl.com/builds/latest.json");
            using var content = res.Content;

            var data = await content.ReadAsStringAsync();
            var jsonobj = JObject.Parse(data);
            var theirhash = jsonobj["commit"].ToString().ToLower();
            if (theirhash != Assemblyinfo.GitHash)
            {
                GameLog.Warning("THIS VERSION OF HYBRASYL IS OUT OF DATE");
                GameLog.Warning(
                    $"You have {Assemblyinfo.GitHash} but {theirhash} is available as of {jsonobj["build_date"]}");
                GameLog.Warning("You can download the new version at https://www.hybrasyl.com/builds/ .");
            }
            else
            {
                GameLog.Info("This version of Hybrasyl is up to date!");
            }
        }
        catch (Exception e)
        {
            ReportException(e);
            GameLog.Error("An error occurred checking if server updates are available {e}", e);
        }
    }

    private static async void GetCommitLog()
    {
        if (Assemblyinfo.GitHash == "unknown")
        {
            GameLog.Error("Git log fetch skipped, git hash not found in assemblyinfo.");
            return;
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Hybrasyl Server");
            using var res =
                await client.GetAsync($"https://api.github.com/repos/hybrasyl/server/commits/{Assemblyinfo.GitHash}");
            using var content = res.Content;

            var data = await content.ReadAsStringAsync();
            var jsonobj = JObject.Parse(data);

            if (res.StatusCode == HttpStatusCode.OK)
                CommitLog = jsonobj["commit"]["message"].ToString();
            else
                CommitLog = "There was an error fetching commit log information from Github. Sorry.";
        }
        catch (Exception e)
        {
            ReportException(e);
            GameLog.Error("Couldn't fetch version information from GitHub: {e}", e);
            CommitLog = "There was an error fetching commit log information from Github. Sorry.";
        }
    }

    public static void LoadCollisions()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var sotp = assembly.GetManifestResourceStream("Hybrasyl.Resources.sotp.dat");
        using (var ms = new MemoryStream())
        {
            sotp.CopyTo(ms);
            Collisions = ms.ToArray();
        }
    }

    /// <summary>
    ///     Check to see if a sprite change should also trigger a collision change. Used to
    ///     determine if a door opening triggers a collision update server side or not. This specifically
    ///     handles the case of doors in Piet and Undine which are 3 tiles wide (and all 3 change graphically)
    ///     but collision updates only occur for two tiles.
    /// </summary>
    /// <param name="sprite">Sprite number.</param>
    /// <returns>true/false indicating whether the sprite should trigger a collision.</returns>
    public static bool IsDoorCollision(ushort sprite)
    {
        if (OpenDoorSprites.ContainsKey(sprite))
            return (Collisions[sprite - 1] & 0x0F) == 0x0F ||
                   (Collisions[OpenDoorSprites[sprite] - 1] & 0x0F) == 0x0F;
        return (Collisions[sprite - 1] & 0x0F) == 0x0F ||
               (Collisions[ClosedDoorSprites[sprite] - 1] & 0x0F) == 0x0F;
    }
}

public static class Crypto
{
    public static string HashString(string value, string hashName)
    {
        var algo = HashAlgorithm.Create(hashName);
        var buffer = Encoding.ASCII.GetBytes(value);
        var hash = algo.ComputeHash(buffer);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
    }
}

public static class Crc16
{
    #region CRC Table 1

    private static readonly byte[] crcTable1 =
    {
        0x00, 0x00, 0x10, 0x21, 0x20, 0x42, 0x30, 0x63, 0x40, 0x84, 0x50, 0xA5, 0x60, 0xC6, 0x70, 0xE7,
        0x81, 0x08, 0x91, 0x29, 0xA1, 0x4A, 0xB1, 0x6B, 0xC1, 0X8C, 0xD1, 0xAD, 0xE1, 0xCE, 0xF1, 0xEF,
        0x12, 0x31, 0x02, 0x10, 0x32, 0x73, 0x22, 0x52, 0x52, 0xB5, 0x42, 0x94, 0x72, 0xF7, 0x62, 0xD6,
        0x93, 0x39, 0x83, 0x18, 0xB3, 0x7B, 0xA3, 0x5A, 0xD3, 0xBD, 0xC3, 0x9C, 0xF3, 0xFF, 0xE3, 0xDE,
        0x24, 0x62, 0x34, 0x43, 0x04, 0x20, 0x14, 0x01, 0x64, 0xE6, 0x74, 0xC7, 0x44, 0xA4, 0x54, 0x85,
        0xA5, 0x6A, 0xB5, 0x4B, 0x85, 0x28, 0x95, 0x09, 0xE5, 0xEE, 0xF5, 0xCF, 0xC5, 0xAC, 0xD5, 0x8D,
        0x36, 0x53, 0x26, 0x72, 0x16, 0x11, 0x06, 0x30, 0x76, 0xD7, 0x66, 0xF6, 0x56, 0x95, 0x46, 0xB4,
        0xB7, 0x5B, 0xA7, 0x7A, 0x97, 0x19, 0x87, 0x38, 0xF7, 0xDF, 0xE7, 0xFE, 0xD7, 0x9D, 0xC7, 0xBC,
        0x48, 0xC4, 0x58, 0xE5, 0x68, 0x86, 0x78, 0xA7, 0x08, 0x40, 0x18, 0x61, 0x28, 0x02, 0x38, 0x23,
        0xC9, 0xCC, 0xD9, 0xED, 0xE9, 0x8E, 0xF9, 0xAF, 0x89, 0x48, 0x99, 0x69, 0xA9, 0x0A, 0xB9, 0x2B,
        0x5A, 0xF5, 0x4A, 0xD4, 0x7A, 0xB7, 0x6A, 0x96, 0x1A, 0x71, 0x0A, 0x50, 0x3A, 0x33, 0x2A, 0x12,
        0xDB, 0xFD, 0xCB, 0xDC, 0xFB, 0xBF, 0xEB, 0x9E, 0x9B, 0x79, 0x8B, 0x58, 0xBB, 0x3B, 0xAB, 0x1A,
        0x6C, 0xA6, 0x7C, 0x87, 0x4C, 0xE4, 0x5C, 0xC5, 0x2C, 0x22, 0x3C, 0x03, 0x0C, 0x60, 0x1C, 0x41,
        0xED, 0xAE, 0xFD, 0x8F, 0xCD, 0xEC, 0xDD, 0xCD, 0xAD, 0x2A, 0xBD, 0x0B, 0x8D, 0x68, 0x9D, 0x49,
        0x7E, 0x97, 0x6E, 0xB6, 0x5E, 0xD5, 0x4E, 0xF4, 0x3E, 0x13, 0x2E, 0x32, 0x1E, 0x51, 0x0E, 0x70,
        0xFF, 0x9F, 0xEF, 0xBE, 0xDF, 0xDD, 0xCF, 0xFC, 0xBF, 0x1B, 0xAF, 0x3A, 0x9F, 0x59, 0x8F, 0x78
    };

    #endregion

    #region CRC Table 2

    private static readonly byte[] crcTable2 =
    {
        0x91, 0x88, 0x81, 0xA9, 0xB1, 0xCA, 0xA1, 0xEB, 0xD1, 0x0C, 0xC1, 0x2D, 0xF1, 0x4E, 0xE1, 0x6F,
        0x10, 0x80, 0x00, 0xA1, 0x30, 0xC2, 0x20, 0xE3, 0x50, 0x04, 0x40, 0x25, 0x70, 0x46, 0x60, 0x67,
        0x83, 0xB9, 0x93, 0x98, 0xA3, 0xFB, 0xB3, 0xDA, 0xC3, 0x3D, 0xD3, 0x1C, 0xE3, 0x7F, 0xF3, 0x5E,
        0x02, 0xB1, 0x12, 0x90, 0x22, 0xF3, 0x32, 0xD2, 0x42, 0x35, 0x52, 0x14, 0x62, 0x77, 0x72, 0x56,
        0xB5, 0xEA, 0xA5, 0xCB, 0x95, 0xA8, 0x85, 0x89, 0xF5, 0x6E, 0xE5, 0x4F, 0xD5, 0x2C, 0xC5, 0x0D,
        0x34, 0xE2, 0x24, 0xC3, 0x14, 0xA0, 0x04, 0x81, 0x74, 0x66, 0x64, 0x47, 0x54, 0x24, 0x44, 0x05,
        0xA7, 0xDB, 0xB7, 0xFA, 0x87, 0x99, 0x97, 0xB8, 0xE7, 0x5F, 0xF7, 0x7E, 0xC7, 0x1D, 0xD7, 0x3C,
        0x26, 0xD3, 0x36, 0xF2, 0x06, 0x91, 0x16, 0xB0, 0x66, 0x57, 0x76, 0x76, 0x46, 0x15, 0x56, 0x34,
        0xd9, 0x4C, 0xC9, 0x6D, 0xF9, 0x0E, 0xE9, 0x2F, 0x99, 0xC8, 0x89, 0xE9, 0xB9, 0x8A, 0xA9, 0xAB,
        0x58, 0x44, 0x48, 0x65, 0x78, 0x06, 0x68, 0x27, 0x18, 0xC0, 0x08, 0xE1, 0x38, 0x82, 0x28, 0xA3,
        0xCB, 0x7D, 0xDB, 0x5C, 0xEB, 0x3F, 0xfB, 0x1E, 0x8B, 0xF9, 0x9B, 0xD8, 0xAB, 0xBB, 0xBB, 0x9A,
        0x4A, 0x75, 0x5A, 0x54, 0x6A, 0x37, 0x7A, 0x16, 0x0A, 0xF1, 0x1A, 0xD0, 0x2A, 0xB3, 0x3A, 0x92,
        0xFD, 0x2E, 0xED, 0x0F, 0xDD, 0x6C, 0xCD, 0x4D, 0xBD, 0xAA, 0xAD, 0x8B, 0x9D, 0xE8, 0x8D, 0xC9,
        0x7C, 0x26, 0x6C, 0x07, 0x5C, 0x64, 0x4C, 0x45, 0x3C, 0xA2, 0x2C, 0x83, 0x1C, 0xE0, 0x0C, 0xC1,
        0xEF, 0x1F, 0xFF, 0x3E, 0xCF, 0x5D, 0xDF, 0x7C, 0xAF, 0x9B, 0xBF, 0xBA, 0x8F, 0xD9, 0x9F, 0xF8,
        0x6E, 0x17, 0x7E, 0x36, 0x4E, 0x55, 0x5E, 0x74, 0x2E, 0x93, 0x3E, 0xB2, 0x0E, 0xD1, 0x1E, 0xF0
    };

    #endregion

    public static ushort Calculate(byte[] buffer)
    {
        byte valueA = 0, valueB = 0;

        for (var i = 0; i < buffer.Length; i += 6)
        for (var ix = 0; ix < 6; ix++)
        {
            byte[] table;

            if ((valueB & 128) != 0)
                table = crcTable2;
            else
                table = crcTable1;

            var valueC = valueB << 1;
            valueB = (byte) (valueA ^ table[valueC++ % 256]);
            valueA = (byte) (buffer[i + ix] ^ table[valueC % 256]);
        }

        byte[] ret = { valueA, valueB };
        Array.Reverse(ret);
        return BitConverter.ToUInt16(ret, 0);
    }
}

public static class Crc32
{
    #region CRC 32 Table

    private static readonly uint[] crc32Table =
    {
        0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
        0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
        0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
        0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
        0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
        0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
        0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
        0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924, 0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
        0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
        0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
        0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
        0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
        0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2, 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
        0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
        0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
        0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
        0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
        0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
        0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
        0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
        0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
        0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
        0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236, 0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
        0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
        0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
        0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
        0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
        0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
        0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
        0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
        0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
        0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
    };

    #endregion


    public static uint ComputeChecksum(byte[] filedata)
    {
        var hash = uint.MaxValue;
        byte data;

        for (var i = 0; i < filedata.Length; ++i)
        {
            data = (byte) (filedata[i] ^ (hash & 0xFF));
            hash = crc32Table[data] ^ (hash >> 0x8);
        }

        return ~hash;
    }

    public static uint Calculate(byte[] data)
    {
        var crc = 0xFFFFFFFF;
        var pos = 0;
        var i = data.Length;

        while (i != 0)
        {
            crc = (crc >> 8) ^ crc32Table[(crc & 0xFF) ^ data[pos++]];
            i--;
        }

        return crc;
    }
}