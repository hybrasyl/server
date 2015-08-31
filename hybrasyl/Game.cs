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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using System.Data.Odbc;
using Hybrasyl.Properties;
using Hybrasyl.XML.Config;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using log4net.Core;
using zlib;
using AssemblyInfo = Hybrasyl.Utility.AssemblyInfo;

namespace Hybrasyl
{
    public static class Game
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly object SyncObj = new object();
        public static IPAddress IpAddress;

        public static Lobby Lobby { get; private set; }
        public static Login Login { get; private set; }
        public static World World { get; private set; }
        public static byte[] ServerTable { get; private set; }
        public static uint ServerTableCrc { get; private set; }
        public static byte[] Notification { get; set; }
        public static uint NotificationCrc { get; set; }
        public static byte[] Collisions { get; set; }

        public static int LogLevel { get; set; }

        public static AssemblyInfo Assemblyinfo  { get; set; }
        private static long Active = 0;

        public static XML.Config.HybrasylConfig Config { get; private set; }

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

        public static void Main(string[] args)
        {
            // Make our window nice and big
            Console.SetWindowSize(140, 36);
            LogLevel = Hybrasyl.Constants.DEFAULT_LOG_LEVEL;
            XDocument config;
            Assemblyinfo = new AssemblyInfo(Assembly.GetEntryAssembly());

            Constants.DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Hybrasyl");

            if (!Directory.Exists(Constants.DataDirectory))
            {
                Logger.InfoFormat("Creating data directory {0}", Constants.DataDirectory);
                try
                {
                    // Create the various directories we need
                    Directory.CreateDirectory(Constants.DataDirectory);
                    Directory.CreateDirectory(Path.Combine(Constants.DataDirectory, "maps"));
                    Directory.CreateDirectory(Path.Combine(Constants.DataDirectory, "scripts"));

                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Can't create data directory: {0}", e.ToString());
                    return;
                }
            }
            
            var hybconfig = Path.Combine(Constants.DataDirectory, "config.xml");

            if (File.Exists(hybconfig))
            {
                var xml = File.ReadAllText(hybconfig);
                HybrasylConfig newConfig;
                Exception parseException;
                if (XML.Config.HybrasylConfig.Deserialize(xml, out newConfig, out parseException))
                    Config = newConfig;
                else
                {
                    Logger.ErrorFormat("Error parsing Hybrasyl configuration: {1}", hybconfig, parseException);
                    Environment.Exit(0);
                }
            }
            else
            {

                Console.ForegroundColor = ConsoleColor.White;

                Console.Write("Welcome to Project Hybrasyl: this is Hybrasyl server {0}\n", Assemblyinfo.Version);
                Console.Write("I need to ask some questions before we can go on. You'll also need to\n");
                Console.Write("make sure that an app.config exists in the Hybrasyl server directory,\n");
                Console.Write("and that the database specified there exists and is properly loaded.\n");
                Console.Write("Otherwise, you're gonna have a bad time.\n\n");
                Console.Write("These questions will only be asked once - if you need to make changes\n");
                Console.Write("in the future, edit config.xml in the Hybrasyl server directory.\n\n");

                Console.Write("Enter this server's IP address, or what IP we should bind to (default is 127.0.0.1): ");
                var serverIp = Console.ReadLine();
                Console.Write("Enter the Lobby Port (default is 2610): ");
                var lobbyPort = Console.ReadLine();
                Console.Write("Enter the Login Port: (default is 2611): ");
                var loginPort = Console.ReadLine();
                Console.Write("Enter the World Port (default is 2612): ");
                var worldPort = Console.ReadLine();

                if (String.IsNullOrEmpty(serverIp))
                    serverIp = "127.0.0.1";

                if (String.IsNullOrEmpty(lobbyPort))
                    lobbyPort = "2610";

                if (String.IsNullOrEmpty(loginPort))
                    loginPort = "2611";

                if (String.IsNullOrEmpty(worldPort))
                    worldPort = "2612";

                Logger.InfoFormat("Using {0}: {1}, {2}, {3}", serverIp, lobbyPort, loginPort, worldPort);

                Console.Write("Now, we will configure the Redis store.\n\n");
                Console.Write("Redis IP or hostname (default is localhost): ");
                var redisHost = Console.ReadLine();

                Console.Write("Redis authentication information (optional - if you don't have these, just hit enter)");
                Console.Write("Username: ");
                var redisUser = Console.ReadLine();

                Console.Write("Password: ");
                var redisPass = Console.ReadLine();

                if (String.IsNullOrEmpty(redisHost))
                    redisHost = "localhost";

                Config = new HybrasylConfig {datastore = {host = redisHost}, network =
                {
                    lobby = new NetworkInfo { bindaddress = serverIp, port = Convert.ToUInt16(lobbyPort) },
                    login = new NetworkInfo { bindaddress = serverIp, port = Convert.ToUInt16(loginPort) },
                    world = new NetworkInfo { bindaddress = serverIp, port = Convert.ToUInt16(worldPort) }
                }};

                if (String.IsNullOrEmpty(redisUser))
                    Config.datastore.username = redisUser;

                if (String.IsNullOrEmpty(redisPass))
                    Config.datastore.username = redisPass;

                Config.SaveToFile(Path.Combine(Constants.DataDirectory, "config.xml"));
            }
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Debug;
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(
                EventArgs.Empty);
            // Set console buffer, so we can scroll back a bunch
            Console.BufferHeight = Int16.MaxValue - 1;

            Logger.InfoFormat("Hybrasyl {0} starting.", Assemblyinfo.Version);
            Logger.InfoFormat("{0} - this program is licensed under the GNU AGPL, version 3.", Assemblyinfo.Copyright);

            LoadCollisions();

            // For right now we don't support binding to different addresses; the support in the XML
            // is for a distant future where that may be desirable.
            IpAddress = IPAddress.Parse(Config.network.lobby.bindaddress); 
            Lobby = new Lobby(Config.network.lobby.port);
            Login = new Login(Config.network.login.port);
            World = new World(Config.network.world.port, Config.datastore);
            World.InitWorld();

            byte[] addressBytes;
            addressBytes = IpAddress.GetAddressBytes();
            Array.Reverse(addressBytes);

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream, Encoding.GetEncoding(949));

            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write(addressBytes);
            writer.Write((byte)(2611 / 256));
            writer.Write((byte)(2611 % 256));
            writer.Write(Encoding.GetEncoding(949).GetBytes("Hybrasyl;Hybrasyl Production\0"));
            writer.Flush();

            var serverTable = stream.ToArray();
            ServerTableCrc = ~Crc32.Calculate(serverTable);
            ServerTable = Zlib.Compress(stream).ToArray();

            writer.Close();
            stream.Close();
            var serverMsg = Path.Combine(Constants.DataDirectory, "server.msg");
            
            if (File.Exists(serverMsg))
            {
                stream = new MemoryStream(Encoding.GetEncoding(949).GetBytes(File.ReadAllText(serverMsg)));

            }
            else
            {
                stream = new MemoryStream(Encoding.GetEncoding(949).GetBytes(String.Format("Welcome to Hybrasyl!\n\nThis is Hybrasyl (version {0}).\n\nFor more information please visit http://www.hybrasyl.com",
                    Assemblyinfo.Version)));
            }

            var notification = stream.ToArray();
            NotificationCrc = ~Crc32.Calculate(notification);
            Notification = Zlib.Compress(stream).ToArray();

            World.StartTimers();
            World.StartQueueConsumer();

            ToggleActive();

            while (true)
            {
                Lobby.AcceptConnection();
                Login.AcceptConnection();
                World.AcceptConnection();
                if (!IsActive())
                    break;
                Thread.Sleep(10);
                
            }
            Logger.Warn("Hybrasyl: all servers shutting down");
            // Server is shutting down. For Lobby and Login, this terminates the TCP listeners;
            // for World, this triggers a logoff for all logged in users and then terminates. After
            // termination, the queue consumer is stopped as well.
            // For a true restart we'll need to do a few other things; stop timers, etc.
            Lobby.Shutdown();
            Login.Shutdown();
            World.Shutdown();
            World.StopQueueConsumer();
            Logger.WarnFormat("Hybrasyl {0}: shutdown complete.", Assemblyinfo.Version);
            Environment.Exit(0);

        }

        private static void LoadCollisions()
        {
            var assembly = Assembly.GetExecutingAssembly();
            Collisions = Resources.sotp;
            //using (var stream = assembly.GetManifestResourceStream("Hybrasyl.Resources.sotp.dat"))
            //{
            //    int length = (int)stream.Length;
            //    Collisions = new byte[length];
            //    stream.Read(Collisions, 0, length);
            //}
        }

        /// <summary>
        /// Check to see if a sprite change should also trigger a collision change. Used to
        /// determine if a door opening triggers a collision update server side or not. This specifically
        /// handles the case of doors in Piet and Undine which are 3 tiles wide (and all 3 change graphically)
        /// but collision updates only occur for two tiles.
        /// </summary>
        /// <param name="sprite">Sprite number.</param>
        /// <returns>true/false indicating whether the sprite should trigger a collision.</returns>
        public static bool IsDoorCollision(ushort sprite)
        {
            if (OpenDoorSprites.ContainsKey(sprite))
            {
                return (
                    ((Game.Collisions[sprite - 1] & 0x0F) == 0x0F) ||
                    ((Game.Collisions[OpenDoorSprites[sprite] - 1] & 0x0F) == 0x0F)
                    );
            }
            else
            {
                return (
                ((Game.Collisions[sprite - 1] & 0x0F) == 0x0F) ||
                ((Game.Collisions[ClosedDoorSprites[sprite] - 1] & 0x0F) == 0x0F)
                );
            }
        }

        public static readonly Dictionary<ushort, ushort> ClosedDoorSprites = new Dictionary<ushort, ushort>
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

        public static readonly Dictionary<ushort, ushort> OpenDoorSprites = new Dictionary<ushort, ushort>
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

        public static readonly Dictionary<ushort, bool> DoorSprites = new Dictionary<ushort, bool>
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

        public static readonly double[,] ElementTable = new double[10, 10]
        {   //  none    fire    water   wind    earth   light   dark    wood    metal   undead
            {   1.00,   1.00,   1.00,   1.00,   1.00,   1.00,   1.00,   1.00,   1.00,   1.00    },  // none
            {   1.00,   1.00,   0.75,   1.50,   1.00,   1.00,   1.00,   1.00,   1.00,   1.50    },  // fire
            {   1.00,   1.50,   1.00,   1.00,   0.75,   1.00,   1.00,   1.00,   1.50,   1.00    },  // water
            {   1.00,   0.75,   1.00,   1.00,   1.50,   1.00,   1.00,   1.00,   0.75,   1.00    },  // wind
            {   1.00,   1.00,   1.50,   0.75,   1.00,   1.00,   1.00,   1.00,   1.00,   0.75    },  // earth
            {   1.00,   1.00,   1.00,   1.00,   1.00,   0.75,   1.50,   1.00,   1.00,   1.00    },  // light
            {   1.00,   1.00,   1.00,   1.00,   1.00,   1.50,   0.75,   1.00,   1.00,   1.00    },  // dark
            {   1.00,   1.00,   1.00,   1.00,   1.00,   1.00,   1.00,   1.00,   1.00,   1.00    },  // wood
            {   1.00,   1.00,   0.75,   1.50,   1.00,   1.00,   1.00,   1.00,   0.75,   1.50    },  // metal
            {   1.00,   0.75,   1.00,   1.00,   1.50,   1.00,   1.00,   1.00,   1.50,   0.75    }   // undead
        };
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
        private static byte[] crcTable1 = new byte[] { 
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
        private static byte[] crcTable2 = new byte[] { 
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

            for (int i = 0; i < buffer.Length; i += 6)
            {
                for (int ix = 0; ix < 6; ix++)
                {
                    byte[] table;

                    if ((valueB & 128) != 0)
                        table = crcTable2;
                    else
                        table = crcTable1;

                    int valueC = valueB << 1;
                    valueB = (byte)(valueA ^ table[valueC++ % 256]);
                    valueA = (byte)(buffer[i + ix] ^ table[valueC % 256]);
                }
            }

            byte[] ret = new byte[] { valueA, valueB };
            Array.Reverse(ret);
            return BitConverter.ToUInt16(ret, 0);
        }
    }

    public static class Crc32
    {
        #region CRC 32 Table
        private static uint[] crc32Table = new uint[] {
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

        public static uint Calculate(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            int pos = 0;
            int i = data.Length;

            while (i != 0)
            {
                crc = (crc >> 8) ^ crc32Table[(crc & 0xFF) ^ data[pos++]];
                i--;
            }

            return crc;
        }
    }

    public static class Zlib
    {
        public static MemoryStream Compress(MemoryStream input)
        {
            var output = new MemoryStream();

            using (var outZStream = new ZOutputStream(output, zlibConst.Z_DEFAULT_COMPRESSION))
            {
                input.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[2000];
                int len;
                while ((len = input.Read(buffer, 0, 2000)) > 0)
                {
                    outZStream.Write(buffer, 0, len);
                }
                outZStream.Flush();
            }

            return output;
        }
        public static MemoryStream Decompress(MemoryStream input)
        {
            var output = new MemoryStream();

            using (var outZStream = new ZOutputStream(output))
            {
                input.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[2000];
                int len;
                while ((len = input.Read(buffer, 0, 2000)) > 0)
                {
                    outZStream.Write(buffer, 0, len);
                }
                outZStream.Flush();
            }

            return output;
        }
    }
}
