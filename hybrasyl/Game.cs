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
using System.CommandLine.Help;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hybrasyl.Internals.CommandLine;
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
    public static string CommitLog { get; private set; } = "There was an error fetching commit log information from GitHub. Sorry.";

    public static ServerConfig ActiveConfiguration { get; set; }
    public static string WorldDataDirectory { get; set; }
    public static string DataDirectory { get; set; }
    public static string LogDirectory { get; set; }
    public static string ActiveConfigurationName { get; set; }

    public static T GetServerByGuid<T>(Guid g) where T : Server
    {
        if (Servers.TryGetValue(g, out var server))
            return (T)server;
        return null;
    }

    public static T GetDefaultServer<T>() where T : Server =>
        Servers.Values.FirstOrDefault(predicate: x => x is T && x.Default) as T;

    public static Guid GetDefaultServerGuid<T>() where T : Server =>
        Servers.FirstOrDefault(predicate: x => x.Value is T && x.Value.Default).Value?.Guid ?? Guid.Empty;

    public static void RegisterServer(Server s, bool defaultServer = true)
    {
        s.Default = defaultServer;
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

    private static string DefaultDataDir =>
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "hybrasyl");
    private static string DefaultWorldDir => Path.Join(DefaultDataDir, "world");

    // <summary>Hybrasyl, a DOOMVAS-compatible MMO server</summary>
    public static int Main(string[] args)
    {
        var dataOption = new Option<string>("--dataDir", "-d")
        {
            HelpName = "DIRPATH",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("HYB_DATA_DIR") ?? DefaultDataDir,
            Description =
                @"[$HYB_DATA_DIR] The data directory to be used for the server. Defaults to ~/hybrasyl on non-Windows, or %USERPROFILE%\hybrasyl on Windows."
        };
        dataOption.Validators.Add(result =>
        {
            var dir = result.GetValue(dataOption);
            if (!Path.Exists(dir))
                result.AddError($"The data directory {dir} does not exist or cannot be read");
        });

        var worldDataOption = new Option<string>("--worldDataDir", "-w")
        {
            DefaultValueFactory =
                _ => Environment.GetEnvironmentVariable("HYB_WORLD_DIR") ?? DefaultWorldDir,
            HelpName = "DIRPATH",
            Description = @"[$HYB_WORLD_DIR] The XML data directory to be used for the server. Defaults to DATADIR\xml"
        };
        worldDataOption.Validators.Add(result =>
        {
            var dir = result.GetValue(worldDataOption);
            if (!Path.Exists(dir))
                result.AddError($"The world data directory {dir} does not exist or cannot be read");
        });

        var logdirOption = new Option<string>("--logDir", "-l")
        {
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("HYB_LOG_DIR"),
            HelpName = "DIRPATH",
            Description = @"[$HYB_LOG_DIR] The directory for log output from the server. If undefined, logs are send to stdout."
        };

        logdirOption.Validators.Add(result =>
        {
            var dir = result.GetValue(logdirOption);
            if (!string.IsNullOrEmpty(dir) && !Path.Exists(dir))
                result.AddError($"The log directory {dir} does not exist or cannot be read");
        });

        var configName = new Option<string>("--config", "-c")
        {
            DefaultValueFactory = (_) => Environment.GetEnvironmentVariable("HYB_CONFIG_NAME") ?? "default",
            Description = "[$HYB_CONFIG_NAME] The named configuration to use in the world directory. Defaults to default",
            HelpName = "CONFIG_NAME"
        };


        var configFile = new Option<string>("--configFile", "-cf")
        {
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("HYB_CONFIG_FILE") ?? string.Empty,
            Description = "[$HYB_CONFIG_FILE] A path to a configuration file that will be used for the server, instead of a config file in a world repository",
            HelpName = "CONFIG_FILE_PATH"
        };
        configFile.Validators.Add(result =>
        {
            var file = result.GetValue(configFile);
            if (!string.IsNullOrEmpty(file) && !File.Exists(file))
                result.AddError($"The specified config file {file} could not be found or read");
        });

        var redisPort = new Option<int>("--redisPort", "-rpt")
        {
            DefaultValueFactory = _ =>
                int.TryParse(Environment.GetEnvironmentVariable("HYB_REDIS_PORT"), out var value) ? value : 6379,
            Description =
                "[$HYB_REDIS_PORT] The port to use for Redis. Overrides any setting in config xml. Defaults to 6379 in the absence of any other config.",
            HelpName = "PORT_NUMBER"
        };
        redisPort.Validators.Add(result =>
        {
            var port = result.GetValue(redisPort);
            if (port > 65535)
                result.AddError("Redis port value must be between 0 and 65535");
        });

        var redisHost = new Option<string>("--redisHost", "-rh")
        {
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("HYB_REDIS_HOST") ?? string.Empty,
            Description = "[$HYB_REDIS_HOST] The redis server to use. Overrides any setting in config xml. Defaults to localhost in the absence of any other config.",
            HelpName = "HOST_OR_ADDRESS"
        };

        var redisDb = new Option<int>("--redisDb", "-rdb")
        {
            Description =
                "[$HYB_REDIS_DB] The redis DB to use. Overrides any setting in config xml. Defaults to 0 in the absence of any other config.",
            HelpName = "DB_NUMBER"
        };
        redisDb.Validators.Add(result =>
            
        {
            var db = result.GetValue(redisDb);
            if (db > 16)
                result.AddError("Redis DB value must be between 0 and 16");
        });

        var redisPassword = new Option<string>("--redisPassword", "-rpw")
        {
            DefaultValueFactory = _ =>
                Environment.GetEnvironmentVariable("HYB_REDIS_PASSWORD") ?? string.Empty,
            Description = "[$HYB_REDIS_PASSWORD] The password to use for Redis. Overrides any setting in config xml.",
            HelpName = "REDIS_PW"
        };


        var rootCommand = new RootCommand("Hybrasyl, a DOOMVAS-compatible MMO server");

        // This is weird, but it is the documented way to do this
        rootCommand.Add(dataOption);
        rootCommand.Add(worldDataOption);
        rootCommand.Add(logdirOption);
        rootCommand.Add(configName);
        rootCommand.Add(configFile);
        rootCommand.Add(redisHost);
        rootCommand.Add(redisPort);
        rootCommand.Add(redisDb);
        rootCommand.Add(redisPassword);

        var helpOption = rootCommand.Options.FirstOrDefault(x => x is HelpOption);
        if (helpOption != null)
            helpOption.Action = new OctagramHelpAction((HelpAction) helpOption.Action!);

        rootCommand.SetAction(parseResult =>
        {
            StartServer(StartupConfig.FromParseResult(parseResult));
        });

        return rootCommand.Parse(args).Invoke();
    }


    public static void StartServer(StartupConfig startupConfig)
    {
        Assemblyinfo = new AssemblyInfo(Assembly.GetEntryAssembly());
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        // Initialize OTel

        using var activity = ActivitySource.StartActivity("Startup");
        activity?.SetTag("host", Dns.GetHostName());

        // Gather our directories from env vars / command line switches

        DataDirectory = startupConfig.DataDir;
        WorldDataDirectory = startupConfig.WorldDataDir;
        LogDirectory = startupConfig.LogDir;
        ActiveConfigurationName = startupConfig.ConfigName;

        // Set our exit handler
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        Log.Information($"Data directory: {DataDirectory}");
        Log.Information($"World directory: {WorldDataDirectory}");
        Log.Information($"Log directory: {LogDirectory}");

        if (!string.IsNullOrEmpty(LogDirectory) && !Path.Exists(LogDirectory))
        {
            Log.Fatal($"The specified log directory {LogDirectory} does not exist or cannot be accessed.");
            Log.Fatal(
                "If this configuration option is set, Hybrasyl cannot start without this path being usable.");
            Thread.Sleep(10000);
            return;
        }

        var manager = new XmlDataManager(WorldDataDirectory);

        try
        {
            Log.Information("Loading xml...");
            manager.LoadData();
            while (true)
            {
                if (manager.IsReady)
                    break;
                Thread.Sleep(1000);
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
            Log.Fatal("It is recommended you look at the example config.xml in the community database");
            Log.Fatal("which can be found at github.com/hybrasyl/ceridwen .");
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

        ServerConfig activeConfiguration = null;

        if (!string.IsNullOrEmpty(startupConfig.ConfigFile))
        {
            if (Path.Exists(startupConfig.ConfigFile))
            {
                if (ServerConfig.LoadFromFile(startupConfig.ConfigFile, out activeConfiguration, out var e))
                {
                    activeConfiguration.Name = "override";
                    manager.Add(activeConfiguration);
                    ActiveConfigurationName = "override";

                }
                else
                {
                    Log.Fatal($"A config file path of {startupConfig.ConfigFile} was specified, but this file could not be processed:");
                    Log.Fatal($"Exception occurred: {e}");
                    Thread.Sleep(10000);
                    return;
                }
            }
            else
            {
                Log.Fatal(
                    $"A config file path of {startupConfig.ConfigFile} was specified but this file either does not exist or cannot be read.");
                Thread.Sleep(10000);
                return;
            }
        }

        if (manager.Count<ServerConfig>() == 0)
        {
            var loadResult = manager.GetLoadResult<ServerConfig>();
            activity?.SetStatus(ActivityStatusCode.Error);

            Log.Fatal("A server configuration file was not found or could not be loaded.");
            Log.Fatal("Please take a look at the server documentation at github.com/hybrasyl/server.");
            Log.Fatal("It is recommended that you look at the example config.xml in the community database");
            Log.Fatal("which can be found at github.com/hybrasyl/ceridwen .");

            if (string.IsNullOrEmpty(startupConfig.ConfigFile))
            {
                Log.Fatal("Using world repository for configuration.");
                Log.Fatal(
                    $"Expected to find config in: {manager.RootPath}{Path.DirectorySeparatorChar}serverconfigs.");
            }
            else
                Log.Fatal(
                    $"A config file of {startupConfig.ConfigFile} was specified but could not be found or was not accessible.");

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

        if (ActiveConfigurationName != "override" && !manager.TryGetValue(ActiveConfigurationName, out activeConfiguration))
        {
            activity?.SetStatus(ActivityStatusCode.Error);

            Log.Fatal(
                $"A server configuration name of {ActiveConfigurationName} was specified but there are no configurations with that name.");
            Log.Fatal(
                $"Active configurations that were found in {manager.RootPath}{Path.DirectorySeparatorChar}serverconfigs: {string.Join(" ", manager.Values<ServerConfig>().Select(selector: x => x.Name))}");
            Log.Fatal(
                "Hybrasyl cannot start without a server configuration, so it will automatically close in 10 seconds.");
            Thread.Sleep(10000);
            return;
        }

        // This should never happen
        if (activeConfiguration == null)
        {
            Log.Fatal("Active configuration is null - this should not happen. Aborting!");
            return;
        }

        // Sanity check: ensure our localization exists
        if (!manager.TryGetValue(activeConfiguration.Locale, out Localization locale))
        {
            Log.Fatal(
                $"You specified a locale of {activeConfiguration.Locale}, but there are no locales with that name.");
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
        activeConfiguration.Init();
        activeConfiguration.Constants ??= new ServerConstants();
        // Set our active configuration to the one we just loaded
        ActiveConfiguration = activeConfiguration;

        // Configure logging 
        GameLog.LogInit(LogDirectory, activeConfiguration.Logging);

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
        {
            // Allow an envvar to define our external address, which is useful when running in Kubernetes
            // behind a service
            if (activeConfiguration.Network.Login.ExternalAddress.ToLower().StartsWith("envvar:"))
            {
                // ExternalAddress="envvar:k8s-service-name" -> K8S_SERVICE_NAME_SERVICE_HOST
                var svcName = $"{activeConfiguration.Network.Login.ExternalAddress.Split(":").Last().ToUpper()
                    .Replace("-", "_")}_SERVICE_HOST";

                var addr = Environment.GetEnvironmentVariable(svcName);

                if (addr == null)
                    throw new ArgumentNullException(
                        $"Fatal Error: ExternalAddress set to {activeConfiguration.Network.Login.ExternalAddress}, but {svcName} is not defined!");

                try
                {
                    RedirectTarget = IPAddress.Parse(addr);
                    Log.Information("dLogin ");
                }
                catch (Exception ex)
                {
                    Log.Fatal(
                        $"External address set to {activeConfiguration.Network.Login.ExternalAddress}, but {svcName} value {addr} could not be parsed!");
                }
            }
            else
                // We can have a hostname here to support ease of running in Docker; try to naively resolve it
                RedirectTarget = Dns.GetHostAddresses(activeConfiguration.Network.Lobby.ExternalAddress)
                    .FirstOrDefault();
        }

        IpAddress = IPAddress.Parse(activeConfiguration.Network.Lobby.BindAddress);

        Lobby = new Lobby(activeConfiguration.Network.Lobby.Port, true);
        Login = new Login(activeConfiguration.Network.Login.Port, true);

        var redisConnection = new RedisConnection();

        if (!string.IsNullOrEmpty(startupConfig.RedisHost))
        {
            redisConnection.Host = startupConfig.RedisHost;
            redisConnection.Port = startupConfig.RedisPort;
            redisConnection.Database = startupConfig.RedisDb;
            redisConnection.Password = startupConfig.RedisPassword;
            Log.Information($"Using datastore settings from command line args / env vars");
        }
        else if (activeConfiguration.DataStore != null) {
            redisConnection.Host = activeConfiguration.DataStore.Host;
            redisConnection.Port = activeConfiguration.DataStore.Port;
            redisConnection.Database = activeConfiguration.DataStore.Database;
            redisConnection.Password = activeConfiguration.DataStore.Password;
            Log.Information($"Using datastore settings from {activeConfiguration.Filename}");
        }
        else
        {
            Log.Fatal("Datastore settings could not be found. Please either set HYB_REDIS_* environment variables,");
            Log.Fatal("<Datastore> in your config XML, or use command line switches (-rh / -rp etc)");
            Thread.Sleep(10000);
            return;
        }

        Log.Information($"Datastore: {redisConnection.Host}:{redisConnection.Port}/{redisConnection.Database}");

        World = new World(activeConfiguration.Network.World.Port, redisConnection,
            manager, activeConfiguration.Locale, true);

        Lobby.StopToken = CancellationTokenSource.Token;
        Login.StopToken = CancellationTokenSource.Token;
        World.StopToken = CancellationTokenSource.Token;

        _monolith = new Monolith();
        _monolithControl = new MonolithControl();

        if (!World.Init())
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            Log.Fatal(
                "Hybrasyl cannot continue loading. A fatal error occurred while initializing the world.");
            if (!Console.IsInputRedirected)
            {
                Log.Fatal("Press any key to exit.");
                Console.ReadKey();
            }
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
            client.DefaultRequestHeaders.Add("User-Agent", "hybrasyl-server-version-check");
            using var releaseResponse = await client.GetAsync("https://api.github.com/repos/hybrasyl/server/releases/latest");

            var lR = await releaseResponse.Content.ReadAsStringAsync();
            var latestRelease = JObject.Parse(lR);
            var latestTag = latestRelease["tag_name"].ToString();
            var latestReleaseDate = DateTime.Parse(latestRelease["published_at"].ToString());

            using var theirHashResponse =
                await client.GetAsync($"https://api.github.com/repos/hybrasyl/server/git/refs/tags/{latestTag}");

            var theirHash = JObject.Parse(await theirHashResponse.Content.ReadAsStringAsync())["object"]["sha"]
                .ToString().ToLower()[..8];

            if (theirHash != Assemblyinfo.GitHash[..8])
            {
                GameLog.Warning("This version of Hybrasyl may be out of date!");
                GameLog.Warning($"Latest release: {latestTag} - {theirHash}, released {latestReleaseDate} - ours is {Assemblyinfo.GitHash[..8]}");
                GameLog.Warning($"You can download the newest version at https://github.com/hybrasyl/server/releases/tag/{latestTag}.");
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

            if (res.StatusCode == System.Net.HttpStatusCode.OK)
                CommitLog = jsonobj["commit"]["message"].ToString();
        }
        catch (Exception e)
        {
            ReportException(e);
            GameLog.Error("Couldn't fetch version information from GitHub: {e}", e);
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
        if (Sprites.OpenDoorSprites.TryGetValue(sprite, out var doorSprite))
            return (Collisions[sprite - 1] & 0x0F) == 0x0F ||
                   (Collisions[doorSprite - 1] & 0x0F) == 0x0F;
        return (Collisions[sprite - 1] & 0x0F) == 0x0F ||
               (Collisions[Sprites.ClosedDoorSprites[sprite] - 1] & 0x0F) == 0x0F;
    }
}

public static class Crypto
{
    public static string Md5HashString(string value)
    {
        var algo = MD5.Create();
        var buffer = Encoding.ASCII.GetBytes(value);
        var hash = algo.ComputeHash(buffer);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
    }
}