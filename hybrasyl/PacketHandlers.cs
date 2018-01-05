using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Items;
using Hybrasyl.Nations;
using Hybrasyl.Objects;
using log4net;
using log4net.Core;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Hybrasyl.Scripting;
using Castable = Hybrasyl.Castables.Castable;
using Creature = Hybrasyl.Objects.Creature;

namespace Hybrasyl
{
    public partial class World
    {
        public void SetPacketHandlers()
        {
            PacketHandlers[0x05] = WorldPacketHandler_0x05_RequestMap;
            PacketHandlers[0x06] = WorldPacketHandler_0x06_Walk;
            PacketHandlers[0x07] = WorldPacketHandler_0x07_PickupItem;
            PacketHandlers[0x08] = WorldPacketHandler_0x08_DropItem;
            PacketHandlers[0x0B] = WorldPacketHandler_0x0B_ClientExit;
            PacketHandlers[0x0E] = WorldPacketHandler_0x0E_Talk;
            PacketHandlers[0x0F] = WorldPacketHandler_0x0F_UseSpell;
            PacketHandlers[0x10] = WorldPacketHandler_0x10_ClientJoin;
            PacketHandlers[0x11] = WorldPacketHandler_0x11_Turn;
            PacketHandlers[0x13] = WorldPacketHandler_0x13_Attack;
            PacketHandlers[0x18] = WorldPacketHandler_0x18_ShowPlayerList;
            PacketHandlers[0x19] = WorldPacketHandler_0x19_Whisper;
            PacketHandlers[0x1C] = WorldPacketHandler_0x1C_UseItem;
            PacketHandlers[0x1D] = WorldPacketHandler_0x1D_Emote;
            PacketHandlers[0x24] = WorldPacketHandler_0x24_DropGold;
            PacketHandlers[0x29] = WorldPacketHandler_0x29_DropItemOnCreature;
            PacketHandlers[0x2A] = WorldPacketHandler_0x2A_DropGoldOnCreature;
            PacketHandlers[0x2D] = WorldPacketHandler_0x2D_PlayerInfo;
            PacketHandlers[0x2E] = WorldPacketHandler_0x2E_GroupRequest;
            PacketHandlers[0x2F] = WorldPacketHandler_0x2F_GroupToggle;
            PacketHandlers[0x30] = WorldPacketHandler_0x30_MoveUIElement;
            PacketHandlers[0x38] = WorldPacketHandler_0x38_Refresh;
            PacketHandlers[0x39] = WorldPacketHandler_0x39_NPCMainMenu;
            PacketHandlers[0x3A] = WorldPacketHandler_0x3A_DialogUse;
            PacketHandlers[0x3B] = WorldPacketHandler_0x3B_AccessMessages;
            PacketHandlers[0x3E] = WorldPacketHandler_0x3E_UseSkill;
            PacketHandlers[0x3F] = WorldPacketHandler_0x3F_MapPointClick;
            PacketHandlers[0x43] = WorldPacketHandler_0x43_PointClick;
            PacketHandlers[0x44] = WorldPacketHandler_0x44_EquippedItemClick;
            PacketHandlers[0x45] = WorldPacketHandler_0x45_ByteHeartbeat;
            PacketHandlers[0x47] = WorldPacketHandler_0x47_StatPoint;
            PacketHandlers[0x4a] = WorldPacketHandler_0x4A_Trade;
            PacketHandlers[0x4D] = WorldPacketHandler_0x4D_BeginCasting;
            PacketHandlers[0x4E] = WorldPacketHandler_0x4E_CastLine;
            PacketHandlers[0x4F] = WorldPacketHandler_0x4F_ProfileTextPortrait;
            PacketHandlers[0x75] = WorldPacketHandler_0x75_TickHeartbeat;
            PacketHandlers[0x79] = WorldPacketHandler_0x79_Status;
            PacketHandlers[0x7B] = WorldPacketHandler_0x7B_RequestMetafile;
        }

        #region Packet Handlers

        private void WorldPacketHandler_0x05_RequestMap(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            int index = 0;

            for (ushort row = 0; row < user.Map.Y; ++row)
            {
                var x3C = new ServerPacket(0x3C);
                x3C.WriteUInt16(row);
                for (int col = 0; col < user.Map.X * 6; col += 2)
                {
                    x3C.WriteByte(user.Map.RawData[index + 1]);
                    x3C.WriteByte(user.Map.RawData[index]);
                    index += 2;
                }
                user.Enqueue(x3C);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [ProhibitedCondition(PlayerCondition.Paralyzed)]
        private void WorldPacketHandler_0x06_Walk(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var direction = packet.ReadByte();
            if (direction > 3) return;
            user.Walk((Direction)direction);
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x07_PickupItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();

            //var user = client.User;
            //var map = user.Map;

            // Is the player within PICKUP_DISTANCE tiles of what they're trying to pick up?
            if (Math.Abs(x - user.X) > Constants.PICKUP_DISTANCE ||
                Math.Abs(y - user.Y) > Constants.PICKUP_DISTANCE)
                return;

            // Check if inventory slot is valid and empty
            if (slot == 0 || slot > user.Inventory.Size || user.Inventory[slot] != null)
                return;

            // Find the items that are at the pickup area

            var tile = new Rectangle(x, y, 1, 1);

            // We don't want to pick up people
            var pickupObject = user.Map.EntityTree.GetObjects(tile).FindLast(i => i is Gold || i is ItemObject);

            if (pickupObject == null) return;

            string error;
            if (!pickupObject.CanBeLooted(user.Name, out error))
            {
                user.SendSystemMessage(error);
                return;
            }

            // If the add is successful, remove the item from the map quadtree
            if (pickupObject is Gold)
            {
                var gold = (Gold)pickupObject;
                if (user.AddGold(gold))
                {
                    Logger.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        gold.Name, gold.Amount, user.Map.Name, x, y);
                    user.Map.RemoveGold(gold);
                }
            }
            else if (pickupObject is ItemObject)
            {
                var item = (ItemObject)pickupObject;
                if (item.Unique && user.Inventory.Contains(item.TemplateId))
                {
                    user.SendMessage(string.Format("You can't carry any more of those.", item.Name), 3);
                    return;
                }
                if (item.Stackable && user.Inventory.Contains(item.TemplateId))
                {
                    byte existingSlot = user.Inventory.SlotOf(item.TemplateId);
                    var existingItem = user.Inventory[existingSlot];

                    int maxCanGive = existingItem.MaximumStack - existingItem.Count;
                    int quantity = Math.Min(item.Count, maxCanGive);

                    item.Count -= quantity;
                    existingItem.Count += quantity;

                    Logger.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        item.Name, item.Count, user.Map.Name, x, y);
                    user.Map.Remove(item);
                    user.SendItemUpdate(existingItem, existingSlot);

                    if (item.Count == 0) Remove(item);
                    else
                    {
                        user.Map.Insert(item, user.X, user.Y);
                        user.SendMessage(string.Format("You can't carry any more {0}.", item.Name), 3);
                    }
                }
                else
                {
                    Logger.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        item.Name, item.Count, user.Map.Name, x, y);
                    user.Map.Remove(item);
                    user.AddItem(item, slot);
                }
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x08_DropItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();
            var count = packet.ReadInt32();

            Logger.DebugFormat("{0} {1} {2} {3}", slot, x, y, count);

            // Do a few sanity checks
            //
            // Is the distance valid? (Can't drop things beyond
            // MAXIMUM_DROP_DISTANCE tiles away)
            if (Math.Abs(x - user.X) > Constants.PICKUP_DISTANCE ||
                Math.Abs(y - user.Y) > Constants.PICKUP_DISTANCE)
            {
                Logger.ErrorFormat("Request to drop item exceeds maximum distance {0}",
                    Hybrasyl.Constants.MAXIMUM_DROP_DISTANCE);
                return;
            }

            // Is this a valid slot?
            if ((slot == 0) || (slot > Hybrasyl.Constants.MAXIMUM_INVENTORY))
            {
                Logger.ErrorFormat("Slot not valid. Aborting");
                return;
            }

            // Does the player actually have an item in the slot? Does the count in the packet exceed the
            // count in the player's inventory?  Are they trying to drop the item on something that
            // is impassable (i.e. a wall)?
            if ((user.Inventory[slot] == null) || (count > user.Inventory[slot].Count) ||
                (user.Map.IsWall[x, y] == true) || !user.Map.IsValidPoint(x, y))
            {
                Logger.ErrorFormat(
                    "Slot {0} is null, or count {1} exceeds count {2}, or {3},{4} is a wall, or {3},{4} is out of bounds",
                    slot, count, user.Inventory[slot].Count, x, y);
                return;
            }

            ItemObject toDrop = user.Inventory[slot];

            if (toDrop.Stackable && count < toDrop.Count)
            {
                toDrop.Count -= count;
                user.SendItemUpdate(toDrop, slot);

                toDrop = new ItemObject(toDrop);
                toDrop.Count = count;
                Insert(toDrop);
            }
            else
            {
                user.RemoveItem(slot);
            }

            // Are we dropping an item onto a reactor?
            Objects.Reactor reactor;
            var coordinates = new Tuple<byte, byte>((byte)x, (byte)y);
            if (user.Map.Reactors.TryGetValue(coordinates, out reactor))
            {
                reactor.OnDrop(user, toDrop);
            }
            else
                user.Map.AddItem(x, y, toDrop);
        }

        private void WorldPacketHandler_0x0E_Talk(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var isShout = packet.ReadByte();
            var message = packet.ReadString8();

            if (message.StartsWith("/"))
            {
                var args = message.Split(' ');

                #region world's biggest switch statement

                switch (args[0].ToLower())
                {
                    case "/gold":
                        {
                            uint amount;

                            if (args.Length != 2 || !uint.TryParse(args[1], out amount))
                                break;

                            user.Gold = amount;
                            user.UpdateAttributes(StatUpdateFlags.Experience);
                            break;
                        }
                    /**
                     * Give the current user some amount of experience. This experience
                     * will be distributed across a group if the user is in a group, or
                     * passed directly to them if they're not in a group.
                     */
                    case "/hp":
                        {
                            uint hp = 0;
                            if (uint.TryParse(args[1], out hp))
                            {
                                user.Hp = hp;
                                user.UpdateAttributes(StatUpdateFlags.Current);
                            }
                        }
                        break;

                    case "/clearstatus":
                        {
                            user.RemoveAllStatuses();
                            user.Status = PlayerCondition.Alive;
                            user.SendSystemMessage("All statuses cleared.");
                        }
                        break;

                    case "/clearconditions":
                        {
                            user.Status = PlayerCondition.Alive;
                            user.SendSystemMessage("Alive, conditions cleared");
                        }
                        break;

                    case "/applystatus":
                        {
                            var status = args[1];
                            if (status.ToLower() == "poison")
                            {
                                user.ApplyStatus(new PoisonStatus(user, 30, 1, 5));
                            }
                            if (status.ToLower() == "sleep")
                            {
                                user.ApplyStatus(new SleepStatus(user, 30, 1));
                            }
                            if (status.ToLower() == "paralyze")
                            {
                                user.ApplyStatus(new ParalyzeStatus(user, 30, 1));
                            }
                            if (status.ToLower() == "blind")
                            {
                                user.ApplyStatus(new BlindStatus(user, 30, 1));
                            }
                        }
                        break;

                    case "/damage":
                        {
                            var dmg = double.Parse(args[1]);
                            user.Damage(dmg);
                        }
                        break;

                    case "/condition":
                        {
                            user.SendSystemMessage($"Status: {user.Status}");
                        }
                        break;

                    case "/status":
                        {
                            var icon = ushort.Parse(args[1]);

                            user.Enqueue(new ServerPacketStructures.StatusBar { Icon = icon, BarColor = (StatusBarColor)Enum.Parse(typeof(StatusBarColor), args[2]) }.Packet());
                        }
                        break;

                    case "/exp":
                        {
                            uint amount = 0;
                            if (args.Length == 2 && uint.TryParse(args[1], out amount))
                            {
                                user.ShareExperience(amount);
                            }
                        }
                        break;
                    /* Reset a user to level 1, with no level points and no experience. */
                    case "/expreset":
                        {
                            user.LevelPoints = 0;
                            user.Level = 1;
                            user.Experience = 0;
                            user.UpdateAttributes(StatUpdateFlags.Full);
                        }
                        break;

                    case "/group":
                        User newMember = FindUser(args[1]);

                        if (newMember == null)
                        {
                            user.SendMessage("Unknown user in group request.", MessageTypes.SYSTEM);
                            break;
                        }

                        user.InviteToGroup(newMember);
                        break;

                    case "/ungroup":
                        if (user.Group != null)
                        {
                            user.Group.Remove(user);
                        }
                        break;

                    case "/summon":
                        {
                            if (!user.IsPrivileged)
                                return;

                            if (args.Length == 2)
                            {
                                if (!WorldData.ContainsKey<User>(args[1]))
                                {
                                    user.SendMessage("User not logged in.", MessageTypes.SYSTEM);
                                    return;
                                }
                                var target = WorldData.Get<User>(args[1]);
                                if (target.IsExempt)
                                    user.SendMessage("Access denied.", MessageTypes.SYSTEM);
                                else
                                {
                                    target.Teleport(user.Map.Id, user.MapX, user.MapY);
                                    Logger.InfoFormat("GM activity: {0} summoned {1}", user.Name, target.Name);
                                }
                            }
                        }
                        break;

                    case "/nation":
                        {
                            if (args.Length == 2)
                            {
                                if (WorldData.ContainsKey<Nation>(args[1]))
                                {
                                    user.Nation = WorldData.Get<Nation>(args[1]);
                                    user.SendSystemMessage($"Citizenship set to {args[1]}");
                                }
                            }
                        }
                        break;

                    case "/kick":
                        {
                            if (!user.IsPrivileged)
                                return;

                            if (args.Length == 2)
                            {
                                if (!WorldData.ContainsKey<User>(args[1]))
                                {
                                    user.SendMessage("User not logged in.", MessageTypes.SYSTEM);
                                    return;
                                }
                                var target = WorldData.Get<User>(args[1]);
                                if (target.IsExempt)
                                    user.SendMessage("Access denied.", MessageTypes.SYSTEM);
                                else
                                    target.Logoff();
                                Logger.InfoFormat("GM activity: {0} kicked {1}",
                                    user.Name, target.Name);
                            }
                        }
                        break;

                    case "/teleport":
                        {
                            ushort number = ushort.MaxValue;
                            byte x = user.X, y = user.Y;

                            if (args.Length == 2)
                            {
                                if (!ushort.TryParse(args[1], out number))
                                {
                                    if (!WorldData.ContainsKey<User>(args[1]))
                                    {
                                        user.SendMessage("Invalid map number or user name", 3);
                                        return;
                                    }
                                    else
                                    {
                                        var target = WorldData.Get<User>(args[1]);
                                        number = target.Map.Id;
                                        x = target.X;
                                        y = target.Y;
                                    }
                                }
                            }
                            else if (args.Length == 4)
                            {
                                ushort.TryParse(args[1], out number);
                                byte.TryParse(args[2], out x);
                                byte.TryParse(args[3], out y);
                            }

                            if (WorldData.ContainsKey<Map>(number))
                            {
                                var map = WorldData.Get<Map>(number);
                                if (x < map.X && y < map.Y) user.Teleport(number, x, y);
                                else user.SendMessage("Invalid x/y", 3);
                            }
                            else user.SendMessage("Invalid map number", 3);
                        }
                        break;

                    case "/motion":
                        {
                            byte motion;
                            short speed = 20;
                            if (args.Length > 1 && byte.TryParse(args[1], out motion))
                            {
                                if (args.Length > 2) short.TryParse(args[2], out speed);
                                user.Motion(motion, speed);
                            }
                        }
                        break;

                    case "/maplist":
                        {
                            // This is an extremely expensive slash command
                            var searchstring = "";
                            if (args.Length == 1)
                            {
                                user.SendMessage("Usage:   /maplist <searchterm>\nExample: /maplist Mileth - show maps with Mileth in the title\n",
                                    MessageTypes.SLATE);
                                return;
                            }
                            else if (args.Length == 2)
                                searchstring = args[1];
                            else
                                searchstring = string.Join(" ", args, 1, args.Length - 1);

                            Regex searchTerm;
                            try
                            {
                                Logger.InfoFormat("Search term was {0}", searchstring);
                                searchTerm = new Regex(string.Format("{0}", searchstring));
                            }
                            catch
                            {
                                user.SendMessage("Invalid search. Try again or send no options for help.",
                                    MessageTypes.SYSTEM);
                                return;
                            }

                            var queryMaps = from amap in WorldData.Values<Map>()
                                            where searchTerm.IsMatch(amap.Name)
                                            select amap;
                            var result = queryMaps.Aggregate("",
                                (current, map) => current + string.Format("{0} - {1}\n", map.Id, map.Name));

                            if (result.Length > 65400)
                                result = string.Format("{0}\n(Results truncated)", result.Substring(0, 65400));

                            user.SendMessage(string.Format("Search Results\n---------------\n\n{0}",
                                result),
                                MessageTypes.SLATE_WITH_SCROLLBAR);
                        }
                        break;

                    case "/unreadmail":
                        {
                            user.SendSystemMessage(user.UnreadMail ? "Unread mail." : "No unread mail.");
                        }
                        break;

                    case "/effect":
                        {
                            ushort effect;
                            short speed = 100;
                            if (args.Length > 1 && ushort.TryParse(args[1], out effect))
                            {
                                if (args.Length > 2) short.TryParse(args[2], out speed);
                                user.Effect(effect, speed);
                            }
                        }
                        break;

                    case "/sound":
                        {
                            byte sound;
                            if (args.Length > 1 && byte.TryParse(args[1], out sound))
                            {
                                user.SendSound(sound);
                            }
                        }
                        break;

                    case "/music":
                        {
                            byte track;
                            if (args.Length > 1 && byte.TryParse(args[1], out track))
                            {
                                user.Map.Music = track;
                                foreach (var mapuser in user.Map.Users.Values)
                                {
                                    mapuser.SendMusic(track);
                                }
                            }
                        }
                        break;

                    case "/mapmsg":
                        {
                            if (args.Length > 1)
                            {
                                var mapmsg = string.Join(" ", args, 1, args.Length - 1);
                                user.Map.Message = mapmsg;
                                foreach (var mapuser in user.Map.Users.Values)
                                {
                                    mapuser.SendMessage(mapmsg, 18);
                                }
                            }
                        }
                        break;

                    case "/worldmsg":
                        {
                            if (args.Length > 1)
                            {
                                var msg = string.Join(" ", args, 1, args.Length - 1);
                                foreach (var connectedUser in ActiveUsers)
                                {
                                    connectedUser.Value.SendWorldMessage(user.Name, msg);
                                }
                            }
                        }
                        break;

                    case "/class":
                        {
                            var className = string.Join(" ", args, 1, args.Length - 1);
                            int classValue;
                            if (Hybrasyl.Constants.CLASSES.TryGetValue(className, out classValue))
                            {
                                user.Class = (Hybrasyl.Enums.Class)Hybrasyl.Constants.CLASSES[className];
                                user.SendMessage(string.Format("Class set to {0}", className.ToLower()), 0x1);
                            }
                            else
                            {
                                user.SendMessage("I know nothing about that class. Try again.", 0x1);
                            }
                        }
                        break;

                    case "/legend":
                        {
                            var icon = (LegendIcon)Enum.Parse(typeof(LegendIcon), args[1]);
                            var color = (LegendColor)Enum.Parse(typeof(LegendColor), args[2]);
                            var quantity = int.Parse(args[3]);
                            var datetime = DateTime.Parse(args[4]);

                            var legend = string.Join(" ", args, 5, args.Length - 5);
                            user.Legend.AddMark(icon, color, legend, datetime, string.Empty, true, quantity);
                        }
                        break;

                    case "/legendclear":
                        {
                            user.Legend.Clear();
                            user.SendSystemMessage("Legend has been cleared.");
                        }
                        break;

                    case "/level":
                        {
                            byte newLevel;
                            var level = string.Join(" ", args, 1, args.Length - 1);
                            if (!Byte.TryParse(level, out newLevel))
                                user.SendMessage("That's not a valid level, champ.", 0x1);
                            else
                            {
                                user.Level = newLevel > Constants.MAX_LEVEL ? (byte)Constants.MAX_LEVEL : newLevel;
                                user.UpdateAttributes(StatUpdateFlags.Full);
                                user.SendMessage(string.Format("Level changed to {0}", newLevel), 0x1);
                            }
                        }
                        break;

                    case "/attr":
                        {
                            if (args.Length != 3)
                            {
                                return;
                            }
                            byte newStat;
                            if (!Byte.TryParse(args[2], out newStat))
                            {
                                user.SendSystemMessage("That's not a valid value for an attribute, chief.");
                                return;
                            }

                            switch (args[1].ToLower())
                            {
                                case "str":
                                    user.BaseStr = newStat;
                                    break;

                                case "con":
                                    user.BaseCon = newStat;
                                    break;

                                case "dex":
                                    user.BaseDex = newStat;
                                    break;

                                case "wis":
                                    user.BaseWis = newStat;
                                    break;

                                case "int":
                                    user.BaseInt = newStat;
                                    break;

                                default:
                                    user.SendSystemMessage("Invalid attribute, sport.");
                                    break;
                            }
                            user.UpdateAttributes(StatUpdateFlags.Stats);
                        }
                        break;

                    case "/guild":
                        {
                            var guild = string.Join(" ", args, 1, args.Length - 1);
                            // TODO: GUILD SUPPORT
                            //user.guild = guild;
                            user.SendMessage(string.Format("Guild changed to {0}", guild), 0x1);
                        }
                        break;

                    case "/guildrank":
                        {
                            var guildrank = string.Join(" ", args, 1, args.Length - 1);
                            // TODO: GUILD SUPPORT
                            //user.GuildRank = guildrank;
                            user.SendMessage(string.Format("Guild rank changed to {0}", guildrank), 0x1);
                        }
                        break;

                    case "/title":
                        {
                            var title = string.Join(" ", args, 1, args.Length - 1);
                            // TODO: TITLE SUPPORT
                            //user.Title = title;
                            user.SendMessage(string.Format("Title changed to {0}", title), 0x1);
                        }
                        break;

                    case "/debug":
                        {
                            if (!user.IsPrivileged)
                                return;
                            user.SendMessage("Debugging enabled", 3);
                            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Debug;
                            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(
                                EventArgs.Empty);
                            Logger.InfoFormat("Debugging enabled by admin command");
                        }
                        break;

                    case "/nodebug":
                        {
                            if (!user.IsPrivileged)
                                return;
                            user.SendMessage("Debugging disabled", 3);
                            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;
                            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(
                                EventArgs.Empty);
                            Logger.InfoFormat("Debugging disabled by admin command");
                        }
                        break;

                    case "/gcm":
                        {
                            if (!user.IsPrivileged)
                                return;

                            var gcmContents = "Contents of Global Connection Manifest\n";
                            var userContents = "Contents of User Dictionary\n";
                            var ActiveUserContents = "Contents of ActiveUsers Concurrent Dictionary\n";
                            foreach (var pair in GlobalConnectionManifest.ConnectedClients)
                            {
                                var serverType = string.Empty;
                                switch (pair.Value.ServerType)
                                {
                                    case ServerTypes.Lobby:
                                        serverType = "Lobby";
                                        break;

                                    case ServerTypes.Login:
                                        serverType = "Login";
                                        break;

                                    default:
                                        serverType = "World";
                                        break;
                                }
                                try
                                {
                                    gcmContents = gcmContents + string.Format("{0}:{1} - {2}:{3}\n", pair.Key,
                                        ((IPEndPoint)pair.Value.Socket.RemoteEndPoint).Address.ToString(),
                                        ((IPEndPoint)pair.Value.Socket.RemoteEndPoint).Port, serverType);
                                }
                                catch
                                {
                                    gcmContents = gcmContents + string.Format("{0}:{1} disposed\n", pair.Key, serverType);
                                }
                            }
                            foreach (var tehuser in WorldData.Values<User>())
                            {
                                userContents = userContents + tehuser.Name + "\n";
                            }
                            foreach (var tehotheruser in ActiveUsersByName)
                            {
                                ActiveUserContents = ActiveUserContents +
                                                     string.Format("{0}: {1}\n", tehotheruser.Value, tehotheruser.Key);
                            }

                            // Report to the end user
                            user.SendMessage(
                                string.Format("{0}\n\n{1}\n\n{2}", gcmContents, userContents, ActiveUserContents),
                                MessageTypes.SLATE_WITH_SCROLLBAR);
                        }
                        break;

                    case "/item":
                        {
                            int count;
                            string itemName;

                            Logger.DebugFormat("/item: Last argument is {0}", args.Last());
                            Regex integer = new Regex(@"^\d+$");

                            if (integer.IsMatch(args.Last()))
                            {
                                count = Convert.ToInt32(args.Last());
                                itemName = string.Join(" ", args, 1, args.Length - 2);
                                Logger.InfoFormat("Admin command: Creating item {0} with count {1}", itemName, count);
                            }
                            else
                            {
                                count = 1;
                                itemName = string.Join(" ", args, 1, args.Length - 1);
                            }

                            // HURR O(N) IS MY FRIEND
                            // change this to use itemcatalog pls
                            foreach (var template in WorldData.Values<Item>())
                            {
                                if (template.Name.Equals(itemName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    var item = CreateItem(template.Id);
                                    if (count > item.MaximumStack)
                                        item.Count = item.MaximumStack;
                                    else
                                        item.Count = count;
                                    Insert(item);
                                    user.AddItem(item);
                                }
                            }
                        }
                        break;

                    case "/magicval":
                        {
                            var valueName = args[1];
                            var value = args[2];
                            var property = typeof(User).GetProperty(valueName);
                            property.SetValue(user, Convert.ToByte(value));
                            user.SendSystemMessage(string.Format("Magic value {0} set to {1}", valueName, value));
                            user.UpdateAttributes(StatUpdateFlags.Full);
                        }
                        break;

                    case "/skill":
                        {
                            string skillName;

                            Logger.DebugFormat("/skill: Last argument is {0}", args.Last());
                            Regex integer = new Regex(@"^\d+$");

                            skillName = string.Join(" ", args, 1, args.Length - 1);

                            Castable skill = WorldData.GetByIndex<Castable>(skillName);
                            user.AddSkill(skill);
                        }
                        break;

                    case "/spell":
                        {
                            string spellName;

                            Logger.DebugFormat("/skill: Last argument is {0}", args.Last());
                            Regex integer = new Regex(@"^\d+$");

                            spellName = string.Join(" ", args, 1, args.Length - 1);

                            Castable spell = WorldData.GetByIndex<Castable>(spellName);
                            user.AddSpell(spell);
                        }
                        break;

                    case "/spawn":
                        {
                            string creatureName;
                            Logger.DebugFormat("/skill Last argument is {0}", args.Last());

                            creatureName = string.Join(" ", args, 1, args.Length - 1);
                            Game.World.WorldData.Get<Creature>("Bee");

                            Creature creature = new Creature()
                            {
                                Sprite = 1,
                                World = Game.World,
                                Map = user.Map,
                                Level = 1,
                                DisplayText = "TestMob",
                                BaseHp = 10000,
                                Hp = 10000,
                                BaseMp = 1,
                                Name = "TestMob",
                                Id = 90210,
                                BaseStr = 3,
                                BaseCon = 3,
                                BaseDex = 3,
                                BaseInt = 3,
                                BaseWis = 3,
                                X = user.X,
                                Y = user.Y
                            };
                            Game.World.WorldData.Get<Map>(500).InsertCreature(creature);
                            user.SendVisibleCreature(creature);
                        }
                        break;

                    case "/master":
                        {
                            //if (!user.IsPrivileged)
                            //    return;

                            user.IsMaster = !user.IsMaster;
                            user.SendMessage(user.IsMaster ? "Mastership granted" : "Mastership removed", 3);
                        }
                        break;

                    case "/mute":
                        {
                            if (!user.IsPrivileged)
                                return;
                            var charTarget = string.Join(" ", args, 1, args.Length - 1);
                            var userObj = FindUser(charTarget);
                            if (userObj != null)
                            {
                                userObj.IsMuted = true;
                                userObj.Save();
                                user.SendMessage(string.Format("{0} is now muted.", userObj.Name), 0x1);
                            }
                            else
                            {
                                user.SendMessage("That Aisling is not in Temuair.", 0x01);
                            }
                        }
                        break;

                    case "/time":
                        {
                            var time = HybrasylTime.Now();
                            user.SendMessage(time.ToString(), 0x1);
                        }
                        break;

                    case "/timeconvert":
                        {
                            var target = args[1].ToLower();
                            Logger.DebugFormat("timeconvert: {0}", target);

                            if (target == "aisling")
                            {
                                try
                                {
                                    var datestring = string.Join(" ", args, 2, args.Length - 2);
                                    var hybrasylTime = HybrasylTime.Fromstring(datestring);
                                    user.SendSystemMessage(HybrasylTime.ConvertToTerran(hybrasylTime).ToString("o"));
                                }
                                catch (Exception)
                                {
                                    user.SendSystemMessage("Your Aisling time could not be parsed!");
                                }
                            }
                            else if (target == "terran")
                            {
                                try
                                {
                                    var datestring = string.Join(" ", args, 2, args.Length - 2);
                                    var dateTime = DateTime.Parse(datestring);
                                    var hybrasylTime = HybrasylTime.ConvertToHybrasyl(dateTime);
                                    user.SendSystemMessage(hybrasylTime.ToString());
                                }
                                catch (Exception)
                                {
                                    user.SendSystemMessage(
                                        "Your terran time couldn't be parsed, or, you know, something else was wrong");
                                }
                            }
                            else
                            {
                                user.SendSystemMessage("Usage: /timeconvert (aisling|terran) <date>");
                            }
                        }
                        break;

                    case "/unmute":
                        {
                            if (!user.IsPrivileged)
                                return;
                            var charTarget = string.Join(" ", args, 1, args.Length - 1);
                            var userObj = FindUser(charTarget);
                            if (userObj != null)
                            {
                                userObj.IsMuted = false;
                                userObj.Save();
                                user.SendMessage(string.Format("{0} is now unmuted.", userObj.Name), 0x1);
                            }
                            else
                            {
                                user.SendMessage("That Aisling is not in Temuair.", 0x01);
                            }
                        }
                        break;

                    case "/reload":
                        {
                            if (!user.IsPrivileged)
                                return;
                            // Do nothing here for now
                            // This should reload warps, worldwarps, "item templates", worldmaps, and world map points.
                            // This should obviously use the new ControlMessage stuff.
                            user.SendMessage("This feature is not currently implemented.", 0x01);
                        }
                        break;

                    case "/shutdown":
                        {
                            if (!user.IsPrivileged)
                                return;
                            var password = args[1];
                            if (string.Equals(password, Constants.ShutdownPassword))
                            {
                                MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, user.Name));
                            }
                        }
                        break;

                    case "/scripting":
                        {
                            if (!user.IsPrivileged)
                                return;
                            // Valid scripting commands
                            // /scripting (reload|disable|enable|status) [scriptname]

                            if (args.Count() >= 3)
                            {
                                Script script;
                                if (ScriptProcessor.TryGetScript(args[2].Trim(), out script))
                                {
                                    if (args[1].ToLower() == "reload")
                                    {
                                        script.Disabled = true;
                                        if (script.Run())
                                        {
                                            user.SendMessage($"Script {script.Name}: reloaded", 0x01);
                                            script.Disabled = false;
                                        }
                                        else
                                        {
                                            user.SendMessage($"Script {script.Name}: load error, check scripting log", 0x01);
                                        }
                                    }
                                    else if (args[1].ToLower() == "enable")
                                    {
                                        script.Disabled = false;
                                        user.SendMessage($"Script {script.Name}: enabled", 0x01);
                                    }
                                    else if (args[1].ToLower() == "disable")
                                    {
                                        script.Disabled = true;
                                        user.SendMessage($"Script {script.Name}: disabled", 0x01);
                                    }
                                    else if (args[1].ToLower() == "status")
                                    {
                                        var scriptStatus = string.Format("{0}:", script.Name);
                                        string errorSummary = "--- Error Summary ---\n";

                                        if (script.LastRuntimeError == string.Empty &&
                                            script.CompilationError == string.Empty)
                                            errorSummary = string.Format("{0} no errors", errorSummary);
                                        else
                                        {
                                            if (script.CompilationError != string.Empty)
                                                errorSummary = string.Format("{0} compilation error: {1}", errorSummary,
                                                    script.CompilationError);
                                            if (script.LastRuntimeError != string.Empty)
                                                errorSummary = string.Format("{0} runtime error: {1}", errorSummary,
                                                    script.LastRuntimeError);
                                        }

                                        // Report to the end user
                                        user.SendMessage(string.Format("{0}\n\n{1}", scriptStatus, errorSummary),
                                            MessageTypes.SLATE_WITH_SCROLLBAR);
                                    }
                                }
                                else
                                {
                                    user.SendMessage(string.Format("Script {0} not found!", args[2]), 0x01);
                                }
                            }
                        }
                        break;

                    case "/rollchar":
                        {
                            // /rollchar <class> <level>
                            // Allows you to "roll a new character" of the desired Class and Level, to see his resulting HP and MP.

                            if (!user.IsPrivileged)
                                return;

                            string errorMessage = "Command format is: /rollchar <class> <level>";
                            byte level = 1;

                            if (args.Length != 3)
                            {
                                user.SendMessage(errorMessage, 0x1);
                            }
                            else if (!Enum.IsDefined(typeof(Enums.Class), args[1]))
                            {
                                user.SendMessage("Invalid class. " + errorMessage, 0x1);
                            }
                            else if (!byte.TryParse(args[2], out level) || level < 1 || level > Constants.MAX_LEVEL)
                            {
                                user.SendMessage("Invalid level. " + errorMessage, 0x1);
                            }
                            else
                            {
                                // Create a fake User, and level him up to the desired level

                                var testUser = new User(this, new Client());
                                testUser.Map = new Map();
                                testUser.BaseHp = 50;
                                testUser.BaseMp = 50;
                                testUser.Class = (Enums.Class)Enum.Parse(typeof(Enums.Class), args[1]);
                                testUser.Level = 1;

                                while (testUser.Level < level)
                                {
                                    testUser.GiveExperience(testUser.ExpToLevel);
                                }

                                user.SendMessage(string.Format("{0}, Level {1}, Hp: {2}, Mp: {3}", testUser.Class, testUser.Level, testUser.BaseHp, testUser.BaseMp), 0x01);
                            }
                        }
                        break;

                        #endregion world's biggest switch statement
                }
            }
            else
            {
                if (user.Dead)
                {
                    user.SendSystemMessage("Your voice is carried away by a sudden wind.");
                    return;
                }

                if (isShout == 1)
                {
                    user.Shout(message);
                }
                else
                {
                    user.Say(message);
                }
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [ProhibitedCondition(PlayerCondition.Paralyzed)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x0F_UseSpell(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var target = packet.ReadUInt32();

            user.UseSpell(slot, target);
            user.Status ^= PlayerCondition.Casting;
        }

        private void WorldPacketHandler_0x0B_ClientExit(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var endSignal = packet.ReadByte();

            if (endSignal == 1)
            {
                var x4C = new ServerPacket(0x4C);
                x4C.WriteByte(0x01);
                x4C.WriteUInt16(0x00);
                user.Enqueue(x4C);
            }
            else
            {
                long connectionId;

                //user.Save();
                user.UpdateLogoffTime();
                user.Map.Remove(user);
                if (user.Grouped)
                {
                    user.Group.Remove(user);
                }
                Remove(user);
                DeleteUser(user.Name);
                user.SendRedirectAndLogoff(this, Game.Login, user.Name);

                if (ActiveUsersByName.TryRemove(user.Name, out connectionId))
                {
                    ((IDictionary)ActiveUsers).Remove(connectionId);
                }
                Logger.InfoFormat("cid {0}: {1} leaving world", connectionId, user.Name);
            }
        }

        private void WorldPacketHandler_0x10_ClientJoin(Object obj, ClientPacket packet)
        {
            var connectionId = (long)obj;

            var seed = packet.ReadByte();
            var keyLength = packet.ReadByte();
            var key = packet.Read(keyLength);
            var name = packet.ReadString8();
            var id = packet.ReadUInt32();

            var redirect = ExpectedConnections[id];

            if (!redirect.Matches(name, key, seed)) return;

            ((IDictionary)ExpectedConnections).Remove(id);

            User loginUser;

            if (!TryGetUser(name, out loginUser)) return;

            loginUser.AssociateConnection(this, connectionId);
            loginUser.SetEncryptionParameters(key, seed, name);
            loginUser.UpdateLoginTime();
            loginUser.Inventory.RecalculateWeight();
            loginUser.Equipment.RecalculateWeight();
            loginUser.RecalculateBonuses();
            loginUser.UpdateAttributes(StatUpdateFlags.Full);
            loginUser.SendInventory();
            loginUser.SendEquipment();
            loginUser.SendSkills();
            loginUser.SendSpells();
            loginUser.SetCitizenship();

            Insert(loginUser);
            Logger.DebugFormat("Elapsed time since login: {0}", loginUser.SinceLastLogin);

            if (loginUser.Dead)
            {
                loginUser.Teleport("Chaotic Threshold", 10, 10);
            }
            else if (loginUser.Nation.SpawnPoints.Count != 0 &&
                loginUser.SinceLastLogin > Hybrasyl.Constants.NATION_SPAWN_TIMEOUT)
            {
                var spawnpoint = loginUser.Nation.RandomSpawnPoint;
                if (spawnpoint != null) loginUser.Teleport(spawnpoint.MapName, spawnpoint.X, spawnpoint.Y);
                else loginUser.Teleport((ushort)500, (byte)50, (byte)(50));
            }
            else if (WorldData.ContainsKey<Map>(loginUser.Location.MapId))
            {
                loginUser.Teleport(loginUser.Location.MapId, (byte)loginUser.Location.X, (byte)loginUser.Location.Y);
            }
            else
            {
                // Handle any weird cases where a map someone exited on was deleted, etc
                // This "default" of Mileth should be set somewhere else
                loginUser.Teleport((ushort)500, (byte)50, (byte)50);
            }

            Logger.DebugFormat("Adding {0} to hash", loginUser.Name);
            AddUser(loginUser);
            ActiveUsers[connectionId] = loginUser;
            ActiveUsersByName[loginUser.Name] = connectionId;
            Logger.InfoFormat("cid {0}: {1} entering world", connectionId, loginUser.Name);
            Logger.InfoFormat($"{loginUser.SinceLastLoginstring}");
            // If the user's never logged off before (new character), don't display this message.
            if (loginUser.Login.LastLogoff != default(DateTime))
            {
                loginUser.SendSystemMessage($"It has been {loginUser.SinceLastLoginstring} since your last login.");
            }
            loginUser.SendSystemMessage(HybrasylTime.Now().ToString());
            loginUser.Reindex();
        }

        [ProhibitedCondition(PlayerCondition.Frozen)]
        private void WorldPacketHandler_0x11_Turn(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var direction = packet.ReadByte();
            if (direction > 3) return;
            user.Turn((Direction)direction);
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [ProhibitedCondition(PlayerCondition.Paralyzed)]
        private void WorldPacketHandler_0x13_Attack(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.AssailAttack(user.Direction);
        }

        private void WorldPacketHandler_0x18_ShowPlayerList(Object obj, ClientPacket packet)
        {
            var me = (User)obj;

            var list = from user in WorldData.Values<User>()
                       orderby user.IsMaster descending, user.Level descending, user.BaseHp + user.BaseMp * 2 descending, user.Name ascending
                       select user;

            var listPacket = new ServerPacket(0x36);
            listPacket.WriteUInt16((ushort)list.Count());
            listPacket.WriteUInt16((ushort)list.Count());

            foreach (var user in list)
            {
                int levelDifference = Math.Abs((int)user.Level - me.Level);

                listPacket.WriteByte((byte)user.Class);
                // TODO: GUILD SUPPORT
                //if (!string.IsNullOrEmpty(me.Guild) && user.Guild == me.Guild) listPacket.WriteByte(84);
                if (levelDifference <= 5) listPacket.WriteByte(151);
                else listPacket.WriteByte(255);

                listPacket.WriteByte((byte)user.GroupStatus);
                listPacket.WriteString8(""); //user.Title);
                listPacket.WriteBoolean(user.IsMaster);
                listPacket.WriteString8(user.Name);
            }
            me.Enqueue(listPacket);
        }

        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x19_Whisper(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var size = packet.ReadByte();
            var target = Encoding.GetEncoding(949).GetString(packet.Read(size));
            var msgsize = packet.ReadByte();
            var message = Encoding.GetEncoding(949).GetString(packet.Read(msgsize));

            // "!!" is the special character sequence for group whisper. If this is the
            // target, the message should be sent as a group whisper instead of a standard
            // whisper.
            if (target == "!!")
            {
                user.SendGroupWhisper(message);
            }
            else
            {
                user.SendWhisper(target, message);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x1C_UseItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();

            Logger.DebugFormat("Updating slot {0}", slot);

            if (slot == 0 || slot > Constants.MAXIMUM_INVENTORY) return;

            var item = user.Inventory[slot];

            if (item == null) return;

            switch (item.ItemObjectType)
            {
                case Enums.ItemObjectType.CanUse:
                    if (item.Durability == 0)
                    {
                        user.SendSystemMessage("This item is too badly damaged to use.");
                        return;
                    }

                    item.Invoke(user);
                    if (item.Count == 0)
                        user.RemoveItem(slot);
                    else
                        user.SendItemUpdate(item, slot);
                    break;

                case Enums.ItemObjectType.CannotUse:
                    user.SendMessage("You can't use that.", 3);
                    break;

                case Enums.ItemObjectType.Equipment:
                    {
                        if (item.Durability == 0)
                        {
                            user.SendSystemMessage("This item is too badly damaged to use.");
                            return;
                        }
                        // Check item requirements here before we do anything rash
                        string message;
                        if (!item.CheckRequirements(user, out message))
                        {
                            // If an item can't be equipped, CheckRequirements will return false
                            // and also set the appropriate message for us via out
                            user.SendMessage(message, 3);
                            return;
                        }
                        Logger.DebugFormat("Equipping {0}", item.Name);
                        // Remove the item from inventory, but we don't decrement its count, as it still exists.
                        user.RemoveItem(slot);

                        // Handle gauntlet / ring special cases
                        if (item.EquipmentSlot == ClientItemSlots.Gauntlet)
                        {
                            Logger.DebugFormat("item is gauntlets");
                            // First, is the left arm slot occupied?
                            if (user.Equipment[ClientItemSlots.LArm] != null)
                            {
                                if (user.Equipment[ClientItemSlots.RArm] == null)
                                {
                                    // Right arm slot is empty; use it
                                    user.AddEquipment(item, ClientItemSlots.RArm);
                                }
                                else
                                {
                                    // Right arm slot is in use; replace LArm with item
                                    var olditem = user.Equipment[ClientItemSlots.LArm];
                                    user.RemoveEquipment(ClientItemSlots.LArm);
                                    user.AddItem(olditem, slot);
                                    user.AddEquipment(item, ClientItemSlots.LArm);
                                }
                            }
                            else
                            {
                                user.AddEquipment(item, ClientItemSlots.LArm);
                            }
                        }
                        else if (item.EquipmentSlot == ClientItemSlots.Ring)
                        {
                            Logger.DebugFormat("item is ring");

                            // First, is the left ring slot occupied?
                            if (user.Equipment[ClientItemSlots.LHand] != null)
                            {
                                if (user.Equipment[ClientItemSlots.RHand] == null)
                                {
                                    // Right ring slot is empty; use it
                                    user.AddEquipment(item, ClientItemSlots.RHand);
                                }
                                else
                                {
                                    // Right ring slot is in use; replace LHand with item
                                    var olditem = user.Equipment[ClientItemSlots.LHand];
                                    user.RemoveEquipment(ClientItemSlots.LHand);
                                    user.AddItem(olditem, slot);
                                    user.AddEquipment(item, ClientItemSlots.LHand);
                                }
                            }
                            else
                            {
                                user.AddEquipment(item, ClientItemSlots.LHand);
                            }
                        }
                        else if (item.EquipmentSlot == ClientItemSlots.FirstAcc ||
                                 item.EquipmentSlot == ClientItemSlots.SecondAcc ||
                                 item.EquipmentSlot == ClientItemSlots.ThirdAcc)
                        {
                            if (user.Equipment.FirstAcc == null)
                                user.AddEquipment(item, ClientItemSlots.FirstAcc);
                            else if (user.Equipment.SecondAcc == null)
                                user.AddEquipment(item, ClientItemSlots.SecondAcc);
                            else if (user.Equipment.ThirdAcc == null)
                                user.AddEquipment(item, ClientItemSlots.ThirdAcc);
                            else
                            {
                                // Remove first accessory
                                var oldItem = user.Equipment.FirstAcc;
                                user.RemoveEquipment(ClientItemSlots.FirstAcc);
                                user.AddEquipment(item, ClientItemSlots.FirstAcc);
                                user.AddItem(oldItem, slot);
                                user.Show();
                            }
                        }
                        else
                        {
                            var equipSlot = item.EquipmentSlot;
                            var oldItem = user.Equipment[equipSlot];

                            if (oldItem != null)
                            {
                                Logger.DebugFormat(" Attemping to equip {0}", item.Name);
                                Logger.DebugFormat("..which would unequip {0}", oldItem.Name);
                                Logger.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                                user.RemoveEquipment(equipSlot);
                                user.AddItem(oldItem, slot);
                                user.AddEquipment(item, equipSlot);
                                user.Show();
                                Logger.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                            }
                            else
                            {
                                Logger.DebugFormat("Attemping to equip {0}", item.Name);
                                user.AddEquipment(item, equipSlot);
                                user.Show();
                            }
                        }
                    }
                    break;
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x1D_Emote(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var emote = packet.ReadByte();
            if (emote <= 35)
            {
                emote += 9;
                user.Motion(emote, 120);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x24_DropGold(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var amount = packet.ReadUInt32();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();

            Logger.DebugFormat("{0} {1} {2}", amount, x, y);
            // Do a few sanity checks

            // Is the distance valid? (Can't drop things beyond
            // MAXIMUM_DROP_DISTANCE tiles away)
            if (Math.Abs(x - user.X) > Constants.PICKUP_DISTANCE ||
                Math.Abs(y - user.Y) > Constants.PICKUP_DISTANCE)
            {
                Logger.ErrorFormat("Request to drop gold exceeds maximum distance {0}",
                    Hybrasyl.Constants.MAXIMUM_DROP_DISTANCE);
                return;
            }

            // Does the amount in the packet exceed the
            // amount of gold the player has?  Are they trying to drop the item on something that
            // is impassable (i.e. a wall)?
            if ((amount > user.Gold) || (x >= user.Map.X) || (y >= user.Map.Y) ||
                (x < 0) || (y < 0) || (user.Map.IsWall[x, y]))
            {
                Logger.ErrorFormat("Amount {0} exceeds amount {1}, or {2},{3} is a wall, or {2},{3} is out of bounds",
                    amount, user.Gold, x, y);
                return;
            }

            var toDrop = new Gold(amount);
            user.RemoveGold(amount);

            Insert(toDrop);

            // Are we dropping an item onto a reactor?
            Objects.Reactor reactor;
            var coordinates = new Tuple<byte, byte>((byte)x, (byte)y);
            if (user.Map.Reactors.TryGetValue(coordinates, out reactor))
            {
                reactor.OnDrop(user, toDrop);
            }
            else
                user.Map.AddGold(x, y, toDrop);
        }

        private void WorldPacketHandler_0x2D_PlayerInfo(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.SendProfile();
        }

        /**
         * Handle user-initiated grouping requests. There are a number of mechanisms in the client
         * that send this packet, but generally amount to one of three serverside actions:
         *    1) Request that the user join my group (stage 0x02).
         *    2) Leave the group I'm currently in (stage 0x02).
         *    3) Confirm that I'd like to accept a group request (stage 0x03).
         * The general flow here consists of the following steps:
         * Check to see if we should add the partner to the group, or potentially remove them
         *    1) if user and partner are already in the same group.
         *    2) Check to see if the partner is open for grouping. If not, fail.
         *    3) Sending a group request to the group you're already in == ungroup request in USDA.
         *    4) If the user's already grouped, they can't join this group.
         *    5) Send them a dialog and have them explicitly accept.
         *    6) If accepted, join group (see stage 0x03).
         */
        [ProhibitedCondition(PlayerCondition.InComa)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x2E_GroupRequest(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            // stage:
            //   0x02 = user is sending initial request to invitee
            //   0x03 = invitee responds with a "yes"
            byte stage = packet.ReadByte();
            User partner = FindUser(packet.ReadString8());

            // TODO: currently leaving five bytes on the table here. There's probably some
            // additional work that needs to happen though I haven't been able to determine
            // what those bytes represent yet...

            if (partner == null)
            {
                return;
            }

            switch (stage)
            {
                // Stage 0x02 means that a user is sending an initial request to the invitee.
                // That means we need to check whether the user is a valid candidate for
                // grouping, and send the confirmation dialog if so.
                case 0x02:
                    Logger.DebugFormat("{0} invites {1} to join a group.", user.Name, partner.Name);

                    // Remove the user from the group. Kinda logically weird beside all of this other stuff
                    // so it may be worth restructuring but it should be accurate as-is.
                    if (partner.Grouped && user.Grouped && partner.Group == user.Group)
                    {
                        Logger.DebugFormat("{0} leaving group.", user.Name);
                        user.Group.Remove(partner);
                        return;
                    }

                    // Now we know that we're trying to add this person to the group, not remove them.
                    // Let's find out if they're eligible and invite them if so.
                    if (partner.Grouped)
                    {
                        user.SendMessage(string.Format("{0} is already in a group.", partner.Name), MessageTypes.SYSTEM);
                        return;
                    }

                    if (!partner.Grouping)
                    {
                        user.SendMessage(string.Format("{0} is not accepting group invitations.", partner.Name), MessageTypes.SYSTEM);
                        return;
                    }

                    // Send partner a dialog asking whether they want to group (opcode 0x63).
                    ServerPacket response = new ServerPacket(0x63);
                    response.WriteByte((byte)0x01);
                    response.WriteString8(user.Name);
                    response.WriteByte(0);
                    response.WriteByte(0);

                    partner.Enqueue(response);
                    break;
                // Stage 0x03 means that the invitee has responded with a "yes" to the grouping
                // request. We need to add them to the original user's group. Note that in this
                // case the partner sent the original invitation.
                case 0x03:
                    Logger.Debug("Invitation accepted. Grouping.");
                    partner.InviteToGroup(user);
                    break;

                default:
                    Logger.Error("Unknown GroupRequest stage. No action taken.");
                    break;
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x2F_GroupToggle(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            // If the user is in a group, they must leave (in particular going from true to false,
            // but in no case should you be able to hold a group across this transition).
            if (user.Grouped)
            {
                user.Group.Remove(user);
            }

            user.Grouping = !user.Grouping;
            user.Save();

            // TODO: Is there any packet content that needs to be used on the server? It appears there
            // are extra bytes coming through but not sure what purpose they serve.
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x2A_DropGoldOnCreature(Object obj, ClientPacket packet)
        {
            var goldAmount = packet.ReadUInt32();
            var targetId = packet.ReadUInt32();

            var user = (User)obj;
            // If the object is a creature or an NPC, simply give them the item, otherwise,
            // initiate an exchange

            WorldObject target;
            if (!user.World.Objects.TryGetValue(targetId, out target))
                return;

            if (user.Map.Objects.Contains((VisibleObject)target))
            {
                if (target is User)
                {
                    // Initiate exchange and put gold in it
                    var playerTarget = (User)target;

                    // Pre-flight checks
                    if (!Exchange.StartConditionsValid(user, playerTarget))
                    {
                        user.SendSystemMessage("You can't do that.");
                        return;
                    }
                    if (!playerTarget.IsAvailableForExchange)
                    {
                        user.SendMessage("They can't do that right now.", MessageTypes.SYSTEM);
                        return;
                    }
                    if (!user.IsAvailableForExchange)
                    {
                        user.SendMessage("You can't do that right now.", MessageTypes.SYSTEM);
                    }
                    // Start exchange
                    var exchange = new Exchange(user, playerTarget);
                    exchange.StartExchange();
                    exchange.AddGold(user, goldAmount);
                }
                else if (target is Creature && user.IsInViewport((VisibleObject)target))
                {
                    // Give gold to Creature and go about our lives
                    var creature = (Creature)target;
                    creature.Gold += goldAmount;
                    user.Gold -= goldAmount;
                    user.UpdateAttributes(StatUpdateFlags.Stats);
                }
                else
                {
                    Logger.DebugFormat("user {0}: invalid drop target");
                }
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x29_DropItemOnCreature(Object obj, ClientPacket packet)
        {
            var itemSlot = packet.ReadByte();
            var targetId = packet.ReadUInt32();
            var quantity = packet.ReadByte();
            var user = (User)obj;

            // If the object is a creature or an NPC, simply give them the item, otherwise,
            // initiate an exchange

            WorldObject target;
            if (!user.World.Objects.TryGetValue(targetId, out target))
                return;

            if (user.Map.Objects.Contains((VisibleObject)target))
            {
                if (target is User)
                {
                    var playerTarget = (User)target;

                    // Pre-flight checks

                    if (!Exchange.StartConditionsValid(user, playerTarget))
                    {
                        user.SendSystemMessage("You can't do that.");
                        return;
                    }
                    if (!playerTarget.IsAvailableForExchange)
                    {
                        user.SendSystemMessage("They can't do that right now.");
                        return;
                    }
                    if (!user.IsAvailableForExchange)
                    {
                        user.SendSystemMessage("You can't do that right now.");
                    }
                    // Initiate exchange and put item in it
                    var exchange = new Exchange(user, playerTarget);
                    exchange.StartExchange();
                    if (user.Inventory[itemSlot] != null && user.Inventory[itemSlot].Count > 1)
                        user.SendExchangeQuantityPrompt(itemSlot);
                    else
                        exchange.AddItem(user, itemSlot, quantity);
                }
                else if (target is Creature && user.IsInViewport((VisibleObject)target))
                {
                    var creature = (Creature)target;
                    var item = user.Inventory[itemSlot];
                    if (item != null)
                    {
                        if (user.RemoveItem(itemSlot))
                        {
                            creature.Inventory.AddItem(item);
                        }
                        else
                            Logger.WarnFormat("0x29: Couldn't remove item from inventory...?");
                    }
                }
            }
        }

        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x30_MoveUIElement(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var window = packet.ReadByte();
            var oldSlot = packet.ReadByte();
            var newSlot = packet.ReadByte();

            // For right now we ignore the other cases (moving a skill or spell)

            //0 inv, 1 skills, 2 spells. are there others?

            if (window > 2)
                return;

            Logger.DebugFormat("Moving {0} to {1}", oldSlot, newSlot);

            //0 inv, 1 spellbook, 2 skillbook (WHAT FUCKING INTERN REVERSED THESE??).
            switch (window)
            {
                case 0:
                    {
                        var inventory = user.Inventory;
                        if (oldSlot == 0 || oldSlot > Constants.MAXIMUM_INVENTORY || newSlot == 0 || newSlot > Constants.MAXIMUM_INVENTORY || (inventory[oldSlot] == null && inventory[newSlot] == null)) return;
                        user.SwapItem(oldSlot, newSlot);
                        break;
                    }
                case 1:
                    {
                        var book = user.SpellBook;
                        if (oldSlot == 0 || oldSlot > Constants.MAXIMUM_BOOK || newSlot == 0 || newSlot > Constants.MAXIMUM_BOOK || (book[oldSlot] == null && book[newSlot] == null)) return;
                        user.SwapCastable(oldSlot, newSlot, book);
                        break;
                    }
                case 2:
                    {
                        var book = user.SkillBook;
                        if (oldSlot == 0 || oldSlot > Constants.MAXIMUM_BOOK || newSlot == 0 || newSlot > Constants.MAXIMUM_BOOK || (book[oldSlot] == null && book[newSlot] == null)) return;
                        user.SwapCastable(oldSlot, newSlot, book);
                        break;
                    }

                default:
                    break;
            }

            // Is the slot invalid? Does at least one of the slots contain an item?
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x3B_AccessMessages(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var response = new ServerPacket(0x31);
            var action = packet.ReadByte();

            switch (action)
            {
                case 0x01:
                    {
                        // Display board list
                        response.WriteByte(0x01);

                        // TODO: This has the potential to be a somewhat expensive operation, optimize this.
                        var boardList =
                            WorldData.Values<Board>().Where(mb => mb.CheckAccessLevel(user.Name, BoardAccessLevel.Read));

                        // Mail is always the first board and has a fixed id of 0
                        response.WriteUInt16((ushort)(boardList.Count() + 1));
                        response.WriteUInt16(0);
                        response.WriteString8("Mail");
                        foreach (var board in boardList)
                        {
                            response.WriteUInt16((ushort)board.Id);
                            response.WriteString8(board.DisplayName);
                        }
                        response.TransmitDelay = 600; // This is so the 'w' key in the client works
                                                      // Without this, the messaging panel is a jittery piece of crap that never opens
                    }
                    break;

                case 0x02:
                    {
                        // Get message list
                        var boardId = packet.ReadUInt16();
                        var startPostId = packet.ReadInt16();

                        if (boardId == 0)
                        {
                            user.Enqueue(user.Mailbox.RenderToPacket());
                            return;
                        }
                        else
                        {
                            Board board;
                            if (WorldData.TryGetValueByIndex<Board>(boardId, out board))
                            {
                                user.Enqueue(board.RenderToPacket());
                                return;
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                case 0x03:
                    {
                        // Get message
                        var boardId = packet.ReadUInt16();
                        var postId = packet.ReadInt16();
                        var messageId = postId - 1;
                        var offset = packet.ReadSByte();
                        Message message = null;
                        var error = string.Empty;
                        if (boardId == 0)
                        {
                            // Mailbox access
                            switch (offset)
                            {
                                case 0:
                                    {
                                        // postId is the exact message
                                        if (postId >= 0 && postId <= user.Mailbox.Messages.Count)
                                            message = user.Mailbox.Messages[messageId];
                                        else
                                            error = "That post could not be found.";
                                        break;
                                    }
                                case 1:
                                    {
                                        // Client clicked "prev", which hilariously means "newer"
                                        // postId in this case is the next message
                                        if (postId > user.Mailbox.Messages.Count)
                                            error = "There are no newer messages.";
                                        else
                                        {
                                            var messageList = user.Mailbox.Messages.GetRange(messageId,
                                                user.Mailbox.Messages.Count - messageId);
                                            message = messageList.Find(m => m.Deleted == false);

                                            if (message == null)
                                                error = "There are no newer messages.";
                                        }
                                    }
                                    break;

                                case -1:
                                    {
                                        // Client clicked "next", which means "older"
                                        // postId is previous message
                                        if (postId < 0)
                                            error = "There are no older messages.";
                                        else
                                        {
                                            var messageList = user.Mailbox.Messages.GetRange(0, postId);
                                            messageList.Reverse();
                                            message = messageList.Find(m => m.Deleted == false);
                                            if (message == null)
                                                error = "There are no older messages.";
                                        }
                                    }
                                    break;

                                default:
                                    {
                                        error = "Invalid offset (nice try, chief)";
                                    }
                                    break;
                            }
                            if (message != null)
                            {
                                user.Enqueue(message.RenderToPacket());
                                message.Read = true;
                                user.UpdateAttributes(StatUpdateFlags.Secondary);
                                return;
                            }
                            response.WriteByte(0x06);
                            response.WriteBoolean(false);
                            response.WriteString8(error);
                        }
                        else
                        {
                            // Get board message
                            Board board;
                            if (WorldData.TryGetValue<Board>(boardId, out board))
                            {
                                // TODO: handle this better
                                if (!board.CheckAccessLevel(user.Name, BoardAccessLevel.Read))
                                    return;

                                switch (offset)
                                {
                                    case 0:
                                        {
                                            // postId is the exact message
                                            if (postId >= 0 && postId <= board.Messages.Count)
                                            {
                                                if (board.Messages[messageId].Deleted)
                                                    error = "There is no such message.";
                                                else
                                                    message = board.Messages[messageId];
                                            }
                                            else
                                                error = "That post could not be found.";
                                            break;
                                        }
                                    case 1:
                                        {
                                            // Client clicked "prev", which hilariously means "newer"
                                            // postId in this case is the next message
                                            if (postId > board.Messages.Count)
                                                error = "There are no newer messages.";
                                            else
                                            {
                                                var messageList = board.Messages.GetRange(messageId,
                                                    board.Messages.Count - messageId);
                                                message = messageList.Find(m => m.Deleted == false);

                                                if (message == null)
                                                    error = "There are no newer messages.";
                                            }
                                        }
                                        break;

                                    case -1:
                                        {
                                            // Client clicked "next", which means "older"
                                            // postId is previous message
                                            if (postId < 0)
                                                error = "There are no older messages.";
                                            else
                                            {
                                                var messageList = board.Messages.GetRange(0, postId);
                                                messageList.Reverse();
                                                message = messageList.Find(m => m.Deleted == false);
                                                if (message == null)
                                                    error = "There are no older messages.";
                                            }
                                        }
                                        break;

                                    default:
                                        {
                                            error = "Invalid offset (nice try, chief)";
                                        }
                                        break;
                                }
                                if (message != null)
                                {
                                    user.Enqueue(message.RenderToPacket());
                                    message.Read = true;
                                    return;
                                }
                                response.WriteByte(0x06);
                                response.WriteBoolean(false);
                                response.WriteString8(error);
                            }
                        }
                    }
                    break;
                // Send message
                case 0x04:
                    {
                        var boardId = packet.ReadUInt16();
                        var subject = packet.ReadString8();
                        var body = packet.ReadString16();
                        Board board;
                        response.WriteByte(0x06); // Generic board response
                        if (DateTime.Now.Ticks - user.LastMailboxMessageSent < Constants.SEND_MESSAGE_COOLDOWN)
                        {
                            response.WriteBoolean(false);
                            response.WriteString8("Try waiting a moment before sending another message.");
                        }
                        if (WorldData.TryGetValue(boardId, out board))
                        {
                            if (board.CheckAccessLevel(user.Name, BoardAccessLevel.Write))
                            {
                                if (board.ReceiveMessage(new Message(board.Name, user.Name, subject, body)))
                                {
                                    response.WriteBoolean(true);
                                    response.WriteString8("Your message has been sent.");
                                }
                                else
                                {
                                    if (board.IsLocked)
                                    {
                                        response.WriteBoolean(false);
                                        response.WriteString8(
                                            "This board is being cleaned by the Mundanes. Please try again later.");
                                    }
                                    else if (board.Full)
                                    {
                                        response.WriteBoolean(false);
                                        response.WriteString8(
                                            "This board has too many papers nailed to it. Please try again later.");
                                    }
                                }
                            }
                            else
                            {
                                response.WriteBoolean(false);
                                response.WriteString8(
                                    "A strange, ethereal force prohibits you from doing that.");
                            }
                        }
                        else
                        {
                            Logger.WarnFormat("boards: {0} tried to post to non-existent board {1}",
                                user.Name, boardId);
                            response.WriteBoolean(false);
                            response.WriteString8(
                                "...What would you say you're doing here?");
                        }
                    }
                    break;
                // Delete post
                case 0x05:
                    {
                        response.WriteByte(0x07); // Delete post response
                        var boardId = packet.ReadUInt16();
                        var postId = packet.ReadUInt16();
                        Board board;
                        if (WorldData.TryGetValue(boardId, out board))
                        {
                            if (user.IsPrivileged || board.CheckAccessLevel(user.Name, BoardAccessLevel.Moderate))
                            {
                                board.DeleteMessage(postId - 1);
                                response.WriteBoolean(true);
                                response.WriteString8("The message was destroyed.");
                            }
                            else
                            {
                                response.WriteBoolean(false);
                                response.WriteString8("You can't do that.");
                            }
                        }
                        else
                        {
                            Logger.WarnFormat("boards: {0} tried to post to non-existent board {1}",
                                user.Name, boardId);
                            response.WriteBoolean(false);
                            response.WriteString8(
                                "...What would you say you're doing here?");
                        }
                    }
                    break;

                case 0x06:
                    {
                        // Send mail (which one might argue, ye olde DOOMVAS protocol designers, is a type of message)

                        var boardId = packet.ReadUInt16();
                        var recipient = packet.ReadString8();
                        var subject = packet.ReadString8();
                        var body = packet.ReadString16();
                        response.WriteByte(0x06); // Send post response
                        User recipientUser;

                        if (WorldData.TryGetValue(recipient, out recipientUser))
                        {
                            try
                            {
                                if (recipientUser.Mailbox.ReceiveMessage(new Message(recipientUser.Name, user.Name, subject,
                                    body)))
                                {
                                    response.WriteBoolean(true); // Post was successful
                                    response.WriteString8("Your letter was sent.");
                                    Logger.InfoFormat("mail: {0} sent message to {1}", user.Name, recipientUser.Name);
                                    MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MailNotifyUser,
                                        recipientUser.Name));
                                }
                                else
                                {
                                    response.WriteBoolean(true);
                                    response.WriteString8("{0}'s mailbox is full. Your message was discarded. Sorry!");
                                }
                            }
                            catch (MessageStoreLocked)
                            {
                                response.WriteBoolean(true);
                                response.WriteString8("{0} cannot receive mail at this time. Sorry!");
                            }
                        }
                        else
                        {
                            response.WriteBoolean(true);
                            response.WriteString8("Sadly, no record of that person exists in the realm.");
                        }
                    }
                    break;

                case 0x07:
                    // Highlight message
                    {
                        // Highlight post (GM only)
                        var boardId = packet.ReadUInt16();
                        var postId = packet.ReadInt16();
                        response.WriteByte(0x08); // Highlight response
                        Board board;

                        if (!user.IsPrivileged)
                        {
                            response.WriteBoolean(false);
                            response.WriteString("You cannot highlight this message.");
                            Logger.WarnFormat("mail: {0} tried to highlight message {1} but isn't GM! Hijinx suspected.",
                                user.Name, postId);
                        }
                        if (WorldData.TryGetValue(boardId, out board))
                        {
                            board.Messages[postId - 1].Highlighted = true;
                            response.WriteBoolean(true);
                            response.WriteString8("The message was highlighted. Good work, chief.");
                        }
                        else
                        {
                            response.WriteBoolean(false);
                            response.WriteString8("...What would you say you're trying to do here?");
                        }
                    }
                    break;

                default:
                    {
                    }
                    break;
            }

            user.Enqueue(response);
        }


        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [ProhibitedCondition(PlayerCondition.Paralyzed)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x3E_UseSkill(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();

            user.UseSkill(slot);
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        private void WorldPacketHandler_0x3F_MapPointClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var target = BitConverter.ToInt64(packet.Read(8), 0);
            Logger.DebugFormat("target bytes are: {0}, maybe", target);

            if (user.IsAtWorldMap)
            {
                MapPoint targetmap;
                if (WorldData.TryGetValue<MapPoint>(target, out targetmap))
                {
                    user.Teleport(targetmap.DestinationMap, targetmap.DestinationX, targetmap.DestinationY);
                }
                else
                {
                    Logger.ErrorFormat(string.Format("{0}: sent us a click to a non-existent map point!",
                        user.Name));
                }
            }
            else
            {
                Logger.ErrorFormat(string.Format("{0}: sent us an 0x3F outside of a map screen!",
                    user.Name));
            }
        }

        private void WorldPacketHandler_0x38_Refresh(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.Refresh();
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x39_NPCMainMenu(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            // We just ignore the header, because, really, what exactly is a 16-bit encryption
            // key plus CRC really doing for you
            var header = packet.ReadDialogHeader();
            var objectType = packet.ReadByte();
            var objectId = packet.ReadUInt32();
            var pursuitId = packet.ReadUInt16();

            Logger.DebugFormat("main menu packet: ObjectType {0}, ID {1}, pursuitID {2}",
                objectType, objectId, pursuitId);

            // Sanity checks
            WorldObject wobj;

            if (Game.World.Objects.TryGetValue(objectId, out wobj))
            {
                // Are we handling a global sequence?
                DialogSequence pursuit;
                VisibleObject clickTarget = wobj as VisibleObject;

                if (pursuitId < Constants.DIALOG_SEQUENCE_SHARED)
                {
                    // Does the sequence exist in the global catalog?
                    try
                    {
                        pursuit = Game.World.GlobalSequences[pursuitId];
                    }
                    catch
                    {
                        Logger.ErrorFormat("{0}: pursuit ID {1} doesn't exist in the global catalog?",
                            wobj.Name, pursuitId);
                        return;
                    }
                }
                else if (pursuitId >= Constants.DIALOG_SEQUENCE_HARDCODED)
                {
                    if (!(wobj is Merchant))
                    {
                        Logger.ErrorFormat("{0}: attempt to use hardcoded merchant menu item on non-merchant",
                            wobj.Name, pursuitId);
                        return;
                    }

                    var menuItem = (MerchantMenuItem)pursuitId;
                    var merchant = (Merchant)wobj;
                    MerchantMenuHandler handler;

                    if (!merchantMenuHandlers.TryGetValue(menuItem, out handler))
                    {
                        Logger.ErrorFormat("{0}: merchant menu item {1} doesn't exist?",
                            wobj.Name, menuItem);
                        return;
                    }

                    if (!merchant.Jobs.HasFlag(handler.RequiredJob))
                    {
                        Logger.ErrorFormat("{0}: merchant does not have required job {1}",
                            wobj.Name, handler.RequiredJob);
                        return;
                    }

                    handler.Callback(user, merchant, packet);
                    return;
                }
                else
                {
                    // This is a local pursuit
                    try
                    {
                        pursuit = clickTarget.Pursuits[pursuitId - Constants.DIALOG_SEQUENCE_SHARED];
                    }
                    catch
                    {
                        Logger.ErrorFormat("{0}: local pursuit {1} doesn't exist?", wobj.Name, pursuitId);
                        return;
                    }
                }
                Logger.DebugFormat("{0}: showing initial dialog for Pursuit {1} ({2})",
                    clickTarget.Name, pursuit.Id, pursuit.Name);
                user.DialogState.StartDialog(clickTarget, pursuit);
                pursuit.ShowTo(user, clickTarget);
            }
            else
            {
                Logger.WarnFormat("specified object ID {0} doesn't exist?", objectId);
                return;
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x3A_DialogUse(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            var header = packet.ReadDialogHeader();
            var objectType = packet.ReadByte();
            var objectID = packet.ReadUInt32();
            var pursuitID = packet.ReadUInt16();
            var pursuitIndex = packet.ReadUInt16();

            Logger.DebugFormat("objectType {0}, objectID {1}, pursuitID {2}, pursuitIndex {3}",
                objectType, objectID, pursuitID, pursuitIndex);

            Logger.DebugFormat("active dialog via state object: pursuitID {0}, pursuitIndex {1}",
                user.DialogState.CurrentPursuitId, user.DialogState.CurrentPursuitIndex);

            if (pursuitID == user.DialogState.CurrentPursuitId && pursuitIndex == user.DialogState.CurrentPursuitIndex)
            {
                // If we get a packet back with the same index and ID, the dialog has been closed.
                Logger.DebugFormat("Dialog closed, resetting dialog state");
                user.DialogState.EndDialog();
                return;
            }

            if ((pursuitIndex > user.DialogState.CurrentPursuitIndex + 1) ||
                (pursuitIndex < user.DialogState.CurrentPursuitIndex - 1))
            {
                Logger.ErrorFormat("Dialog index is outside of acceptable limits (next/prev)");
                return;
            }

            WorldObject wobj;

            if (user.World.Objects.TryGetValue(objectID, out wobj))
            {
                VisibleObject clickTarget = wobj as VisibleObject;
                // Was the previous button clicked? Handle that first
                if (pursuitIndex == user.DialogState.CurrentPursuitIndex - 1)
                {
                    Logger.DebugFormat("Handling prev: client passed index {0}, current index is {1}",
                        pursuitIndex, user.DialogState.CurrentPursuitIndex);

                    if (user.DialogState.SetDialogIndex(clickTarget, pursuitID, pursuitIndex))
                    {
                        user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                        return;
                    }
                }

                // Did the user click next on the last dialog in a sequence?
                // If so, either close the dialog or go to the main menu (main menu by 
                // default

                if (user.DialogState.ActiveDialogSequence.Dialogs.Count() == pursuitIndex)
                {
                    user.DialogState.EndDialog();
                    if (user.DialogState.ActiveDialogSequence.CloseOnEnd)
                    {
                        Logger.DebugFormat("Sending close packet");
                        var p = new ServerPacket(0x30);
                        p.WriteByte(0x0A);
                        p.WriteByte(0x00);
                        user.Enqueue(p);
                    }
                    else
                        clickTarget.DisplayPursuits(user);
                }

                // Is the active dialog an input or options dialog?

                if (user.DialogState.ActiveDialog is OptionsDialog)
                {
                    var paramsLength = packet.ReadByte();
                    var option = packet.ReadByte();
                    var dialog = user.DialogState.ActiveDialog as OptionsDialog;
                    dialog.HandleResponse(user, option, clickTarget);
                }

                if (user.DialogState.ActiveDialog is TextDialog)
                {
                    var paramsLength = packet.ReadByte();
                    var response = packet.ReadString8();
                    var dialog = user.DialogState.ActiveDialog as TextDialog;
                    dialog.HandleResponse(user, response, clickTarget);
                }

                // Did the handling of a response result in our active dialog sequence changing? If so, exit.

                if (user.DialogState.CurrentPursuitId != pursuitID)
                {
                    Logger.DebugFormat("Dialog has changed, exiting");
                    return;
                }

                if (user.DialogState.SetDialogIndex(clickTarget, pursuitID, pursuitIndex))
                {
                    Logger.DebugFormat("Pursuit index is now {0}", pursuitIndex);
                    user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                    return;
                }
                else
                {
                    Logger.DebugFormat("Sending close packet");
                    var p = new ServerPacket(0x30);
                    p.WriteByte(0x0A);
                    p.WriteByte(0x00);
                    user.Enqueue(p);
                    user.DialogState.EndDialog();
                }
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x43_PointClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var clickType = packet.ReadByte();
            Rectangle commonViewport = user.GetViewport();
            // User has clicked an X,Y point
            if (clickType == 3)
            {
                var x = (byte)packet.ReadUInt16();
                var y = (byte)packet.ReadUInt16();
                var coords = new Tuple<byte, byte>(x, y);
                Logger.DebugFormat("coordinates were {0}, {1}", x, y);

                if (user.Map.Doors.ContainsKey(coords))
                {
                    if (user.Map.Doors[coords].Closed)
                        user.SendMessage("It's open.", 0x1);
                    else
                        user.SendMessage("It's closed.", 0x1);

                    user.Map.ToggleDoors(x, y);
                }
                else if (user.Map.Signposts.ContainsKey(coords))
                {
                    user.Map.Signposts[coords].OnClick(user);
                }
                else
                {
                    Logger.DebugFormat("User clicked {0}@{1},{2} but no door/signpost is present",
                        user.Map.Name, x, y);
                }
            }

            // User has clicked on another entity
            else if (clickType == 1)
            {
                var entityId = packet.ReadUInt32();
                Logger.DebugFormat("User {0} clicked ID {1}: ", user.Name, entityId);

                WorldObject clickTarget = new WorldObject();

                if (user.World.Objects.TryGetValue(entityId, out clickTarget))
                {
                    if (clickTarget is User || clickTarget is Merchant)
                    {
                        Type type = clickTarget.GetType();
                        MethodInfo methodInfo = type.GetMethod("OnClick");
                        methodInfo.Invoke(clickTarget, new[] { user });
                    }
                }
            }
            else
            {
                Logger.DebugFormat("Unsupported clickType {0}", clickType);
                Logger.DebugFormat("Packet follows:");
                packet.DumpPacket();
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x44_EquippedItemClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            // This packet is received when a client unequips an item from the detail (a) screen.

            var slot = packet.ReadByte();

            Logger.DebugFormat("Removing equipment from slot {0}", slot);
            var item = user.Equipment[slot];
            if (item != null)
            {
                Logger.DebugFormat("actually removing item");
                user.RemoveEquipment(slot);
                // Add our removed item to our first empty inventory slot
                Logger.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                Logger.DebugFormat("Adding item {0}, count {1} to inventory", item.Name, item.Count);
                user.AddItem(item);
                Logger.DebugFormat("Player weight is now {0}", user.CurrentWeight);
            }
            else
            {
                Logger.DebugFormat("Ignoring useless click on slot {0}", slot);
                return;
            }
        }

        private void WorldPacketHandler_0x45_ByteHeartbeat(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            // Client sends 0x45 response in the reverse order of what the server sends...
            var byteB = packet.ReadByte();
            var byteA = packet.ReadByte();

            if (!user.IsHeartbeatValid(byteA, byteB))
            {
                Logger.InfoFormat("{0}: byte heartbeat not valid, disconnecting", user.Name);
                user.SendRedirectAndLogoff(Game.World, Game.Login, user.Name);
            }
            else
            {
                Logger.DebugFormat("{0}: byte heartbeat valid", user.Name);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x47_StatPoint(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            if (user.LevelPoints > 0)
            {
                switch (packet.ReadByte())
                {
                    case 0x01:
                        user.BaseStr++;
                        break;

                    case 0x04:
                        user.BaseInt++;
                        break;

                    case 0x08:
                        user.BaseWis++;
                        break;

                    case 0x10:
                        user.BaseCon++;
                        break;

                    case 0x02:
                        user.BaseDex++;
                        break;

                    default:
                        return;
                }

                user.LevelPoints--;
                user.UpdateAttributes(StatUpdateFlags.Primary);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void WorldPacketHandler_0x4A_Trade(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var tradeStage = packet.ReadByte();

            if (tradeStage == 0 && user.ActiveExchange != null)
                return;

            if (tradeStage != 0 && user.ActiveExchange == null)
                return;

            if (user.ActiveExchange != null && !user.ActiveExchange.ConditionsValid)
                return;

            switch (tradeStage)
            {
                case 0x00:
                    {
                        // Starting trade
                        var x0PlayerId = packet.ReadInt32();

                        WorldObject target;
                        if (Objects.TryGetValue((uint)x0PlayerId, out target))
                        {
                            if (target is User)
                            {
                                var playerTarget = (User)target;

                                if (Exchange.StartConditionsValid(user, playerTarget))
                                {
                                    user.SendMessage("That can't be done right now.", MessageTypes.SYSTEM);
                                    return;
                                }
                                // Initiate exchange
                                var exchange = new Exchange(user, playerTarget);
                                exchange.StartExchange();
                            }
                        }
                    }
                    break;

                case 0x01:
                    // Add item to trade
                    {
                        // We ignore playerId because we only allow one exchange at a time and we
                        // keep track of the participants on both sides
                        var x1playerId = packet.ReadInt32();
                        var x1ItemSlot = packet.ReadByte();
                        if (user.Inventory[x1ItemSlot] != null && user.Inventory[x1ItemSlot].Count > 1)
                        {
                            // Send quantity request
                            user.SendExchangeQuantityPrompt(x1ItemSlot);
                        }
                        else
                            user.ActiveExchange.AddItem(user, x1ItemSlot);
                    }
                    break;

                case 0x02:
                    // Add item with quantity
                    var x2PlayerId = packet.ReadInt32();
                    var x2ItemSlot = packet.ReadByte();
                    var x2ItemQuantity = packet.ReadByte();
                    user.ActiveExchange.AddItem(user, x2ItemSlot, x2ItemQuantity);
                    break;

                case 0x03:
                    // Add gold to trade
                    var x3PlayerId = packet.ReadInt32();
                    var x3GoldQuantity = packet.ReadUInt32();
                    user.ActiveExchange.AddGold(user, x3GoldQuantity);
                    break;

                case 0x04:
                    // Cancel trade
                    Logger.Debug("Cancelling trade");
                    user.ActiveExchange.CancelExchange(user);
                    break;

                case 0x05:
                    // Confirm trade
                    Logger.Debug("Confirming trade");
                    user.ActiveExchange.ConfirmExchange(user);
                    break;

                default:
                    return;
            }
        }

        private void WorldPacketHandler_0x4D_BeginCasting(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.Status ^= PlayerCondition.Casting;
        }

        private void WorldPacketHandler_0x4E_CastLine(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var textLength = packet.ReadByte();
            var text = packet.Read(textLength);
            if (!user.Status.HasFlag(PlayerCondition.Casting)) return;
            var x0D = new ServerPacketStructures.CastLine() { ChatType = 2, LineLength = textLength, LineText = Encoding.UTF8.GetString(text), TargetId = user.Id };
            var enqueue = x0D.Packet();

            user.SendCastLine(enqueue);

        }

        private void WorldPacketHandler_0x4F_ProfileTextPortrait(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var totalLength = packet.ReadUInt16();
            var portraitLength = packet.ReadUInt16();
            var portraitData = packet.Read(portraitLength);
            var profileText = packet.ReadString16();

            user.PortraitData = portraitData;
            user.ProfileText = profileText;
        }

        private void WorldPacketHandler_0x75_TickHeartbeat(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var serverTick = packet.ReadInt32();
            var clientTick = packet.ReadInt32(); // Dunno what to do with this right now, so we just store it

            if (!user.IsHeartbeatValid(serverTick, clientTick))
            {
                Logger.InfoFormat("{0}: tick heartbeat not valid, disconnecting", user.Name);
                user.SendRedirectAndLogoff(Game.World, Game.Login, user.Name);
            }
            else
            {
                Logger.DebugFormat("{0}: tick heartbeat valid", user.Name);
            }
        }

        private void WorldPacketHandler_0x79_Status(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var status = packet.ReadByte();
            if (status <= 7)
            {
                user.GroupStatus = (UserStatus)status;
            }
        }

        private void WorldPacketHandler_0x7B_RequestMetafile(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var all = packet.ReadBoolean();

            if (all)
            {
                var x6F = new ServerPacket(0x6F);
                x6F.WriteBoolean(all);
                x6F.WriteUInt16((ushort)WorldData.Count<CompiledMetafile>());
                foreach (var metafile in WorldData.Values<CompiledMetafile>())
                {
                    x6F.WriteString8(metafile.Name);
                    x6F.WriteUInt32(metafile.Checksum);
                }
                user.Enqueue(x6F);
            }
            else
            {
                var name = packet.ReadString8();
                if (WorldData.ContainsKey<CompiledMetafile>(name))
                {
                    var file = WorldData.Get<CompiledMetafile>(name);

                    var x6F = new ServerPacket(0x6F);
                    x6F.WriteBoolean(all);
                    x6F.WriteString8(file.Name);
                    x6F.WriteUInt32(file.Checksum);
                    x6F.WriteUInt16((ushort)file.Data.Length);
                    x6F.Write(file.Data);
                    user.Enqueue(x6F);
                }
            }
        }

        #endregion Packet Handlers
    }

    public partial class Lobby
    {
        public void SetPacketHandlers()
        {
            PacketHandlers[0x00] = LobbyPacketHandler_0x00_ClientVersion;
            PacketHandlers[0x57] = LobbyPacketHandler_0x57_ServerTable;
        }

        private void LobbyPacketHandler_0x00_ClientVersion(Client client, ClientPacket packet)
        {
            var x00 = new ServerPacket(0x00);
            x00.WriteByte(0x00);
            x00.WriteUInt32(Game.ServerTableCrc);
            x00.WriteByte(client.EncryptionSeed);
            x00.WriteByte((byte)client.EncryptionKey.Length);
            x00.Write(client.EncryptionKey);
            client.Enqueue(x00);
        }
        private void LobbyPacketHandler_0x57_ServerTable(Client client, ClientPacket packet)
        {
            var mismatch = packet.ReadByte();

            if (mismatch == 1)
            {
                var x56 = new ServerPacket(0x56);
                x56.WriteUInt16((ushort)Game.ServerTable.Length);
                x56.Write(Game.ServerTable);
                Logger.InfoFormat("ServerTable: Sent: {0}", BitConverter.ToString(x56.ToArray()));
                client.Enqueue(x56);
            }
            else
            {
                var server = packet.ReadByte();
                var redirect = new Redirect(client, this, Game.Login, "socket", client.EncryptionSeed, client.EncryptionKey);
                client.Redirect(redirect);
            }
        }
    }

    public partial class Login
    {
        private void SetPacketHandlers()
        {
            PacketHandlers[0x02] = LoginPacketHandler_0x02_CreateA;
            PacketHandlers[0x03] = LoginPacketHandler_0x03_Login;
            PacketHandlers[0x04] = LoginPacketHandler_0x04_CreateB;
            PacketHandlers[0x10] = LoginPacketHandler_0x10_ClientJoin;
            PacketHandlers[0x26] = LoginPacketHandler_0x26_ChangePassword;
            PacketHandlers[0x4B] = LoginPacketHandler_0x4B_RequestNotification;
            PacketHandlers[0x68] = LoginPacketHandler_0x68_RequestHomepage;
        }

        private void LoginPacketHandler_0x02_CreateA(Client client, ClientPacket packet)
        {
            var name = packet.ReadString8();
            var password = packet.ReadString8();
            var email = packet.ReadString8();

            // This string will contain a client-ready message if the provided password
            // isn't valid.
            byte passwordErr = 0x0;

            if (Game.World.PlayerExists(name))
            {
                client.LoginMessage("That name is unavailable.", 3);
            }
            else if (name.Length < 4 || name.Length > 12)
            {
                client.LoginMessage("Names must be between 4 to 12 characters long.", 3);
            }
            else if (!ValidPassword(password, out passwordErr))
            {
                client.LoginMessage(GetPasswordError(passwordErr), 3);
            }
            else if (Regex.IsMatch(name, "^[A-Za-z]{4,12}$"))
            {
                client.NewCharacterName = name;
                client.NewCharacterPassword = HashPassword(password);
                client.LoginMessage("\0", 0);
            }
            else
            {
                client.LoginMessage("Names may only contain letters.", 3);
            }
        }

        private void LoginPacketHandler_0x03_Login(Client client, ClientPacket packet)
        {
            var name = packet.ReadString8();
            var password = packet.ReadString8();
            Logger.DebugFormat("cid {0}: Login request for {1}", client.ConnectionId, name);

            User loginUser;

            if (!World.TryGetUser(name, out loginUser))
            {
                client.LoginMessage("That character does not exist", 3);
                Logger.InfoFormat("cid {0}: attempt to login as nonexistent character {1}", client.ConnectionId, name);

            }
            else if (loginUser.VerifyPassword(password))
            {
                Logger.DebugFormat("cid {0}: password verified for {1}", client.ConnectionId, name);

                if (Game.World.ActiveUsersByName.ContainsKey(name))
                {
                    Logger.InfoFormat("cid {0}: {1} logging on again, disconnecting previous connection",
                        client.ConnectionId, name);
                    World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.LogoffUser, name));
                }

                Logger.DebugFormat("cid {0} ({1}): logging in", client.ConnectionId, name);
                client.LoginMessage("\0", 0);
                client.SendMessage("Welcome to Hybrasyl!", 3);
                Logger.DebugFormat("cid {0} ({1}): sending redirect to world", client.ConnectionId, name);

                var redirect = new Redirect(client, this, Game.World, name, client.EncryptionSeed,
                    client.EncryptionKey);
                Logger.InfoFormat("cid {0} ({1}): login successful, redirecting to world server",
                    client.ConnectionId, name);
                client.Redirect(redirect);
                loginUser.Login.LastLogin = DateTime.Now;
                loginUser.Login.LastLoginFrom = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
                loginUser.Save();
            }
            else
            {
                Logger.WarnFormat("cid {0} ({1}): password incorrect", client.ConnectionId, name);
                client.LoginMessage("Incorrect password", 3);
                loginUser.Login.LastLoginFailure = DateTime.Now;
                loginUser.Login.LoginFailureCount++;
                loginUser.Save();
            }
        }

        private void LoginPacketHandler_0x04_CreateB(Client client, ClientPacket packet)
        {
            if (string.IsNullOrEmpty(client.NewCharacterName) || string.IsNullOrEmpty(client.NewCharacterPassword))
                return;

            var hairStyle = packet.ReadByte();
            var sex = packet.ReadByte();
            var hairColor = packet.ReadByte();

            if (hairStyle < 1)
                hairStyle = 1;

            if (hairStyle > 17)
                hairStyle = 17;

            if (hairColor > 13)
                hairColor = 13;

            if (sex < 1)
                sex = 1;

            if (sex > 2)
                sex = 2;

            if (!Game.World.PlayerExists(client.NewCharacterName))
            {
                var newPlayer = new User();
                newPlayer.Name = client.NewCharacterName;
                newPlayer.Sex = (Sex)sex;
                newPlayer.Location.Direction = Direction.South;
                newPlayer.Location.MapId = 136;
                newPlayer.Location.X = 10;
                newPlayer.Location.Y = 10;
                newPlayer.HairColor = hairColor;
                newPlayer.HairStyle = hairStyle;
                newPlayer.Class = Enums.Class.Peasant;
                newPlayer.Level = 1;
                newPlayer.Experience = 1;
                newPlayer.Level = 1;
                newPlayer.Experience = 0;
                newPlayer.AbilityExp = 0;
                newPlayer.Gold = 0;
                newPlayer.Ability = 0;
                newPlayer.Hp = 50;
                newPlayer.Mp = 50;
                newPlayer.BaseHp = 50;
                newPlayer.BaseMp = 50;
                newPlayer.BaseStr = 3;
                newPlayer.BaseInt = 3;
                newPlayer.BaseWis = 3;
                newPlayer.BaseCon = 3;
                newPlayer.BaseDex = 3;
                newPlayer.Login.CreatedTime = DateTime.Now;
                newPlayer.Password.Hash = client.NewCharacterPassword;
                newPlayer.Password.LastChanged = DateTime.Now;
                newPlayer.Password.LastChangedFrom = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
                newPlayer.Nation = Game.World.DefaultNation;

                IDatabase cache = World.DatastoreConnection.GetDatabase();
                var myPerson = JsonConvert.SerializeObject(newPlayer);
                cache.Set(User.GetStorageKey(newPlayer.Name), myPerson);

                //                    Logger.ErrorFormat("Error saving new player!");
                //                  Logger.ErrorFormat(e.ToString());
                //                client.LoginMessage("Unknown error. Contact admin@hybrasyl.com", 3);
                //          }
                client.LoginMessage("\0", 0);
            }
        }

        private void LoginPacketHandler_0x10_ClientJoin(Client client, ClientPacket packet)
        {
            var seed = packet.ReadByte();
            var keyLength = packet.ReadByte();
            var key = packet.Read(keyLength);
            var name = packet.ReadString8();
            var id = packet.ReadUInt32();

            var redirect = ExpectedConnections[id];
            if (redirect.Matches(name, key, seed))
            {
                ((IDictionary)ExpectedConnections).Remove(id);

                client.EncryptionKey = key;
                client.EncryptionSeed = seed;

                if (redirect.Source is Lobby || redirect.Source is World)
                {
                    var x60 = new ServerPacket(0x60);
                    x60.WriteByte(0x00);
                    x60.WriteUInt32(Game.NotificationCrc);
                    client.Enqueue(x60);
                }
            }

        }

        // Chart for all error password-related error codes were provided by kojasou@ on
        // https://github.com/hybrasyl/server/pull/11.
        private void LoginPacketHandler_0x26_ChangePassword(Client client, ClientPacket packet)
        {
            var name = packet.ReadString8();
            var currentPass = packet.ReadString8();
            // Clientside validation ensures that the same string is typed twice for the new
            // password, and the new password is only sent to the server once. We can assume
            // that they matched if 0x26 request is sent from the client.
            var newPass = packet.ReadString8();

            // TODO: REDIS

            User player;

            if (!World.TryGetUser(name, out player))
            {
                client.LoginMessage(GetPasswordError(0x0E), 0x0E);
                Logger.InfoFormat("cid {0}: Password change attempt on nonexistent player {1}", client.ConnectionId, name);
                return;

            }

            if (player.VerifyPassword(currentPass))
            {

                // Check if the password is valid.
                byte err = 0x00;
                if (ValidPassword(newPass, out err))
                {
                    player.Password.Hash = HashPassword(newPass);
                    player.Password.LastChanged = DateTime.Now;
                    player.Password.LastChangedFrom = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
                    player.Save();
                    // Let the user know the good news.
                    client.LoginMessage("Your password has been changed successfully.", 0x0);
                    Logger.InfoFormat("Password successfully changed for `{0}`", name);
                }
                else
                {
                    client.LoginMessage(GetPasswordError(err), err);
                    Logger.ErrorFormat("Invalid new password proposed during password change attempt for `{0}`", name);
                }
            }
            // The current password is incorrect. Don't allow any changes to happen.
            else
            {
                client.LoginMessage(GetPasswordError(0x0F), 0x0F);
                Logger.ErrorFormat("Invalid current password during password change attempt for `{0}`", name);
            }
        }

        private void LoginPacketHandler_0x4B_RequestNotification(Client client, ClientPacket packet)
        {
            var x60 = new ServerPacket(0x60);
            x60.WriteByte(0x01);
            x60.WriteUInt16((ushort)Game.Notification.Length);
            x60.Write(Game.Notification);
            client.Enqueue(x60);
        }

        private void LoginPacketHandler_0x68_RequestHomepage(Client client, ClientPacket packet)
        {
            var x03 = new ServerPacket(0x66);
            x03.WriteByte(0x03);
            x03.WriteString8("http://www.hybrasyl.com");
            client.Enqueue(x03);
        }
    }
}
