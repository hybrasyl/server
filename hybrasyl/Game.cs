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

using Grpc.Core;
using Hybrasyl.Extensions.Utility;
using Hybrasyl.grpc;
using Hybrasyl.Internals;
using Hybrasyl.Internals.Compression;
using Hybrasyl.Internals.Crc;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Spawning;
using Hybrasyl.Xml.Manager;
using Hybrasyl.Xml.Objects;
using HybrasylGrpc;
using Newtonsoft.Json.Linq;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.CommandLine;
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
using Server = Hybrasyl.Servers.Server;

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

    public static readonly ActivitySource ActivitySource = new("erisco.hybrasyl.server");
    public static TracerProvider TracerProvider;

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

    public static IDisposable Sentry { get; }
    public static bool SentryEnabled { get; }

    public static ServerConfig ActiveConfiguration { get; set; }
    public static string WorldDataDirectory { get; set; }
    public static string DataDirectory { get; set; }
    public static string LogDirectory { get; set; }
    public static string ActiveConfigurationName { get; set; }

    public static T GetServerByGuid<T>(Guid g) where T : Server => Servers.ContainsKey(g) ? (T)Servers[g] : null;

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

    public static void ReportException(Exception e) { }

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
        var dataOption = new Option<string>("--datadir",
            "The data directory to be used for the server. Defaults to ~\\Hybrasyl\\world");

        var worldDataOption = new Option<string>("--worlddatadir",
            "The XML data directory to be used for the server. Defaults to DATADIR\\xml");

        var logdirOption = new Option<string>("--logdir",
            "The directory for log output from the server. Defaults to DATADIR\\logs");

        var configOption = new Option<string>("--config",
            "The named configuration to use in the world directory. Defaults to default");

        var redisHost = new Option<string>("--redisHost",
            "The redis server to use. Overrides any setting in config xml.");

        var redisPort = new Option<int?>("--redisPort",
            "The port to use for Redis. Overrides any setting in config xml.");

        var redisDb = new Option<int?>("--redisDb",
            "The redis DB to use. Overrides any setting in config xml.");

        var redisPassword = new Option<string>("--redisPassword",
            "The password to use for Redis. Overrides any setting in config xml.");

        var rootCommand = new RootCommand("Hybrasyl, a DOOMVAS-compatible MMO server");

        rootCommand.AddOption(dataOption);
        rootCommand.AddOption(worldDataOption);
        rootCommand.AddOption(logdirOption);
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(redisHost);
        rootCommand.AddOption(redisPort);
        rootCommand.AddOption(redisDb);
        rootCommand.AddOption(redisPassword);

        rootCommand.SetHandler(StartServer,
            dataOption, worldDataOption, logdirOption, configOption, redisHost, redisPort, redisDb, redisPassword);

        rootCommand.Invoke(args);
    }

    public static void StartServer(string dataDir = null, string worldDir = null, string logDir = null,
        string configName = null,
        string redisHost = null, int? redisPort = null, int? redisDb = null, string redisPw = null)
    {
        Assemblyinfo = new AssemblyInfo(Assembly.GetEntryAssembly());
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();


        // Initialize OTel


        using var activity = ActivitySource.StartActivity("Startup");

        activity?.SetTag("host", Dns.GetHostName());
        // Gather our directories from env vars / command line switches

        var data = Environment.GetEnvironmentVariable("DATA_DIR") ?? dataDir;
        var world = Environment.GetEnvironmentVariable("WORLD_DIR") ?? worldDir;
        var log = Environment.GetEnvironmentVariable("LOG_DIR") ?? logDir;
        var config = Environment.GetEnvironmentVariable("CONFIG") ?? configName;
        var rHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? redisHost;
        var rawPort = Environment.GetEnvironmentVariable("REDIS_PORT");
        var rawDb = Environment.GetEnvironmentVariable("REDIS_DB");
        var rPort = string.IsNullOrWhiteSpace(rawPort)
            ? redisPort
            : Convert.ToInt32(rawPort);
        var rDb = string.IsNullOrWhiteSpace(rawDb)
            ? redisDb
            : Convert.ToInt32(rawDb);

        var rPw = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? redisHost;

        DataDirectory = string.IsNullOrWhiteSpace(data)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Hybrasyl", "world")
            : data;
        WorldDataDirectory = string.IsNullOrWhiteSpace(world) ? Path.Combine(DataDirectory, "xml") : world;
        LogDirectory = string.IsNullOrWhiteSpace(log) ? Path.Combine(DataDirectory, "logs") : log;
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
            Log.Information("Loading xml...");
            manager.LoadData();
            //            Task.Run(manager.LoadDataAsync).Wait();
            // TODO: improve in library
            while (true)
            {
                if (manager.IsReady)
                    break;
                Thread.Sleep(250);
            }

            Log.Information("Loading xml completed");
            manager.LogResult(Log.Logger);
        }
        catch (FileNotFoundException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
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
            activity?.SetStatus(ActivityStatusCode.Error);
            Log.Fatal($"World data could not be loaded: {ex}");
            Log.Fatal(
                "Hybrasyl cannot start without a working world data directory, so it will automatically close in 10 seconds.");
            return;
        }

        if (manager.Count<ServerConfig>() == 0)
        {
            var loadResult = manager.GetLoadResult<ServerConfig>();
            activity?.SetStatus(ActivityStatusCode.Error);

            Log.Fatal("A server configuration file was not found or could not be loaded.");
            Log.Fatal("Please take a look at the server documentation at github.com/hybrasyl/server.");
            Log.Fatal("We also recommend you look at the example config.xml in the community database");
            Log.Fatal("which can be found at github.com/hybrasyl/ceridwen .");
            Log.Fatal(
                $"We are currently looking in:\n{manager.RootPath}{Path.DirectorySeparatorChar}serverconfigs for a config file.");
            if (loadResult.ErrorCount > 0)
                Log.Fatal("Errors were encountered processing server configuration:");
            foreach (var error in loadResult.Errors) Log.Fatal($"{error.Key}: {error.Value}");

            Log.Fatal(
                "Hybrasyl cannot start without a server configuration file, so it will automatically close in 10 seconds.");
            Thread.Sleep(10000);
            return;
        }

        if (manager.Count<Localization>() == 0)
        {
            var loadResult = manager.GetLoadResult<Localization>();

            Log.Fatal("A localization file could not found or could not be loaded.");
            Log.Fatal("Please take a look at the server documentation at github.com/hybrasyl/server.");
            Log.Fatal("We also recommend you look at the example config.xml in the community database");
            Log.Fatal("which can be found at github.com/hybrasyl/ceridwen .");
            Log.Fatal(
                $"We are currently looking in:\n{manager.RootPath}{Path.DirectorySeparatorChar}localizations for a config file.");
            if (loadResult.ErrorCount > 0)
                Log.Fatal("Errors were encountered processing localizations:");

            foreach (var error in loadResult.Errors) Log.Fatal($"{error.Key}: {error.Value}");

            activity?.SetStatus(ActivityStatusCode.Error);
            Log.Fatal(
                "Hybrasyl cannot start without localizations, so it will automatically close in 10 seconds.");
            Thread.Sleep(10000);
            return;
        }

        if (!manager.TryGetValue(ActiveConfigurationName, out ServerConfig activeConfiguration))
        {
            activity?.SetStatus(ActivityStatusCode.Error);

            Log.Fatal(
                $"You specified a server configuration name of {ActiveConfigurationName}, but there are no configurations with that name.");
            Log.Fatal(
                $"Active configurations that were found in {manager.RootPath}{Path.DirectorySeparatorChar}serverconfigs: {string.Join(" ", manager.Values<ServerConfig>().Select(selector: x => x.Name))}");
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
                "You specified a locale of en_us, but there are no locales with that name.");
            Log.Fatal(
                $"Make sure a localization configuration exists in {manager.RootPath}{Path.DirectorySeparatorChar}localizations and that it matches what is defined in the server configuration.");
            Log.Fatal(
                $"Active configurations that were found in {manager.RootPath}{Path.DirectorySeparatorChar}localizations: {string.Join(" ", manager.Values<Localization>().Select(selector: x => x.Locale))}");
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

        // Configure OTel forwarder, if set

        if (activeConfiguration.ApiEndpoints?.TelemetryEndpoint != null)
        {
            // TODO : actually implement this
            var providerBuilder = Sdk.CreateTracerProviderBuilder()
                .AddSource("erisco.hybrasyl.server")
                .AddConsoleExporter().AddOtlpExporter(configure: opt =>
                {
                    opt.Endpoint = new Uri(activeConfiguration.ApiEndpoints.TelemetryEndpoint.Url);
                });
        }

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
        activity?.SetTag("environment", env);

        LoadCollisions();

        // For right now we don't support binding to different addresses; the support in the XML
        // is for a distant future where that may be desirable.
        if (activeConfiguration.Network.Login.ExternalAddress != null)
            // We can have a hostname here to support ease of running in Docker; try to naively resolve it
            RedirectTarget = Dns.GetHostAddresses(activeConfiguration.Network.Lobby.ExternalAddress).FirstOrDefault();

        IpAddress = IPAddress.Parse(activeConfiguration.Network.Lobby.BindAddress);

        Lobby = new Lobby(activeConfiguration.Network.Lobby.Port, true);
        Login = new Login(activeConfiguration.Network.Login.Port, true);
        var redisConnection = new RedisConnection();
        redisConnection.Host = string.IsNullOrWhiteSpace(rHost) ? activeConfiguration.DataStore.Host : rHost;
        redisConnection.Port = rPort ?? activeConfiguration.DataStore.Port;
        redisConnection.Database = rDb ?? activeConfiguration.DataStore.Database;
        redisConnection.Password = string.IsNullOrWhiteSpace(rPw) ? activeConfiguration.DataStore.Password : rPw;

        World = new World(activeConfiguration.Network.World.Port, redisConnection,
            manager, activeConfiguration.Locale, true);

        Lobby.StopToken = CancellationTokenSource.Token;
        Login.StopToken = CancellationTokenSource.Token;
        World.StopToken = CancellationTokenSource.Token;

        _monolith = new Monolith();
        _monolithControl = new MonolithControl();

        if (!World.InitWorld())
        {
            activity?.SetStatus(ActivityStatusCode.Error);
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
                multiServerTableWriter.Write((byte)1);
                multiServerTableWriter.Write((byte)1);
                multiServerTableWriter.Write(addressBytes);
                multiServerTableWriter.Write((byte)(2611 / 256));
                multiServerTableWriter.Write((byte)(2611 % 256));
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
        _controlThread.Start();
        activity?.SetStatus(ActivityStatusCode.Ok);

        while (!World.WorldState.Ready)
            Thread.Sleep(1000);

        _spawnThread.Start();

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
                var certPath = Path.Join(DataDirectory, activeConfiguration.Network.Grpc.ServerCertificateFile);
                var keyPath = Path.Join(DataDirectory, activeConfiguration.Network.Grpc.ServerKeyFile);

                SslServerCredentials credentials;
                // Load credentials
                try
                {
                    var cert = File.ReadAllText(certPath);
                    var key = File.ReadAllText(keyPath);
                    var keypair_list = new List<KeyCertificatePair> { new(cert, key) };
                    if (activeConfiguration.Network.Grpc.ChainCertificateFile != null)
                    {
                        var chaincerts = File.ReadAllText(Path.Join(DataDirectory,
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
                        new ServerPort(activeConfiguration.Network.Grpc.BindAddress,
                            activeConfiguration.Network.Grpc.Port,
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
