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

namespace Hybrasyl
{
    namespace Enums
    {

        #region Colors and display enumerations
        /// <summary>
        /// how big is ye lantern
        /// </summary>
        public enum LanternSize : byte
        {
            None = 0x00,
            Small = 0x01,
            Large = 0x02
        }

        /// <summary>
        /// Rest positions (standing / sitting / reclining)
        /// </summary>
        public enum RestPosition : byte
        {
            // Seriously, you try naming these
            Standing = 0x00,
            RestPosition1 = 0x01,
            RestPosition2 = 0x02,
            MaximumChill = 0x03
        }   

        /// <summary>
        /// Name display style (on overhead view)
        /// </summary>
        public enum NameDisplayStyle : byte
        {
            GreyHover = 0x00,
            RedAlwaysOn = 0x01,
            GreenHover = 0x02,
            GreyAlwaysOn = 0x03
        }

        /// <summary>
        /// The icon used by a legend mark (the icon is displayed to the left of the legend mark in a player's legend).
        /// </summary>
        public enum LegendIcon
        {
            Community = 0,
            Warrior = 1,
            Rogue = 2,
            Wizard = 3,
            Priest = 4,
            Monk = 5,
            Heart = 6,
            Victory = 7
        }

        /// <summary>
        /// The color of a legend mark. There are a variety of colors not defined here, but the most common ones are.
        /// </summary>
        public enum LegendColor
        {
            Aqua = 1,
            White = 32,
            Pink = 36,
            Peony = 36,
            LightOrange = 50,
            Mahogany = 53,
            Brass = 63,
            LightYellow = 64,
            Yellow = 68,
            LightGreen = 75,
            Teal = 87,
            Blue = 88,
            LightPink = 96,
            DarkPurple = 100,
            Lavender = 105,
            Green = 128,
            Orange = 152,
            Brown = 160,
            Red = 248
        }

        /// <summary>
        /// The color of text. There are a variety of colors not defined here, most common ones are.
        /// </summary>
        public enum TextColor : int
        {
            Red = 62,
            Yellow = 63,
            DarkBlue = 66,
            DarkGrey = 67,
            MediumGrey = 68,
            LightGrey = 69,
            DarkPurple = 70,
            BrightGreen = 71,
            DarkGreen = 72,
            Orange = 73,
            DarkOrange = 74,
            White = 75,
            Blue = 76,
            WhisperBlue = 76,
            Pink = 77
        }

        /// <summary>
        /// Skin colors for a user (player). Some of these are taken from translations.
        /// </summary>
        public enum SkinColor : int
        {
            Basic = 0,
            White = 1,
            Cocoa = 2,
            Orc = 3,
            Yellow = 4,
            Tan = 5,
            Grey = 6,
            LightBlue = 7,
            Orange = 8,
            Purple = 9
        }

        /// <summary>
        /// The color of a status bar (buff/debuff)
        /// </summary>
        public enum StatusBarColor : byte
        {
            Off = 0,
            Blue = 1,
            Green = 2,
            Orange = 3,
            Red = 4,
            White = 5
        }

        public enum PaperType : byte
        {
            Brown = 2,
            Gold = 3,
            Silver = 4
        }

        public enum SpawnCastType
        {
            Offensive,
            Defensive,
            NearDeath,
            OnDeath
        }

        #endregion

        #region Opcode enumerations
        internal static class ExchangeActions
        {
            public const byte Initiate = 0x00;
            public const byte QuantityPrompt = 0x01;
            public const byte ItemUpdate = 0x02;
            public const byte GoldUpdate = 0x03;
            public const byte Cancel = 0x04;
            public const byte Confirm = 0x05;
        }

        //this is a wip
        internal static class OpCodes
        {
            public const byte CryptoKey = 0x00;
            public const byte NewUserCheck = 0x01;
            public const byte LoginMessage = 0x02;
            public const byte Redirect = 0x03;
            public const byte Location = 0x04;
            public const byte UserId = 0x05;
            public const byte MapEdit = 0x06;
            public const byte AddWorldObject = 0x07;
            public const byte Attributes = 0x08;
            public const byte Inventory = 0x09;
            public const byte SystemMessage = 0x0A;
            public const byte UserMove = 0x0B;
            public const byte CreatureMove = 0x0C;
            public const byte CastLine = 0x0D;
            public const byte RemoveWorldObject = 0x0E;
            public const byte AddItem = 0x0F;
            public const byte RemoveItem = 0x10;
            public const byte CreatureDirection = 0x11;
            public const byte Guild = 0x12;
            public const byte HealthBar = 0x13;
            public const byte PasswordCheck = 0x14;
            public const byte MapInfo = 0x15;
            public const byte PacketMapping = 0x16;
            public const byte AddSpell = 0x17;
            public const byte RemoveSpell = 0x18;
            public const byte PlaySound = 0x19;
            public const byte PlayerAnimation = 0x1A;
            public const byte EditablePaper = 0x1B;
            public const byte Icon = 0x1C;
            public const byte ChangeShape = 0x1D;
            public const byte ChangeDay = 0x1E;
            public const byte MapChangeCompleted = 0x1F; //also change weather?
            public const byte ChangeHour = 0x20;
            public const byte SelfSave = 0x21;
            public const byte Refresh = 0x22;
            public const byte BattlefieldInfo = 0x24;
            public const byte DirectMove = 0x25;
            public const byte ActionChange = 0x26;
            public const byte GeneralEffect = 0x27;
            public const byte CloseConnection = 0x28;
            public const byte SpellAnimation = 0x29;
            public const byte AddContainer = 0x2A;
            public const byte RemoveContainer = 0x2B;
            public const byte AddSkill = 0x2C;
            public const byte RemoveSkill = 0x2D;
            public const byte FieldMap = 0x2E;
            public const byte NpcReply = 0x2F;
            public const byte Pursuit = 0x30;
            public const byte Board = 0x31;
            public const byte UserMoveResponse = 0x32;
            public const byte DisplayUser = 0x33;
            public const byte Profile = 0x34;
            public const byte ReadonlyPaper = 0x35;
            public const byte UserList = 0x36;
            public const byte AddEquipment = 0x37;
            public const byte RemoveEquipment = 0x38;
            public const byte SelfProfile = 0x39;
            public const byte StatusBar = 0x3A;
            public const byte PingA = 0x3B;
            public const byte MapData = 0x3C;
            public const byte LevelPoint = 0x3D;
            public const byte UseSkill = 0x3E;
            public const byte Cooldown = 0x3F;
            public const byte SendPatch = 0x40;
            public const byte Exchange = 0x42;
            public const byte ClickObject = 0x43; //worldmapinfo as well?
            public const byte AddUser = 0x44;
            public const byte ItemShop = 0x45;
            public const byte GambleStart = 0x46;
            public const byte TotalUsers = 0x47;
            public const byte CancelCast = 0x48;
            public const byte RequestPortrait = 0x49;
            public const byte Bounce = 0x4B;
            public const byte Reconnect = 0x4C;
            public const byte Emblem = 0x4D;
            public const byte PlayerShop = 0x4F;
            public const byte Manufacture = 0x50;
            public const byte BlockInput = 0x51;
            public const byte ServerClose = 0x53;
            public const byte UnitedSlot = 0x54;
            public const byte MultiServer = 0x56;
            public const byte ServerSelect = 0x57;
            public const byte MapLoadComplete = 0x58;
            public const byte Advertisement = 0x5B;
            public const byte Notification = 0x60;
            public const byte WebBoard = 0x62;
            public const byte Group = 0x63;
            public const byte MiniGame = 0x64;
            public const byte Website = 0x66;
            public const byte MapChangePending = 0x67;
            public const byte PingB = 0x68;
            public const byte Screenshot = 0x6B;
            public const byte LoverName = 0x6D;
            public const byte MetaData = 0x6F;
            public const byte TownMap = 0x7E;
            public const byte QuestInfo = 0x8A;
        }
        #endregion

        #region Messaging types
        public enum PrivateMessageType : int
        {
            Whisper = 0,
            ServerMessage = 1,
            GlobalMessage = 3,
            ClearMessage = 5,
            PopupWithScroll = 8,
            PopupOkCancel = 11,
            UpperRight = 12
        }

        public enum PublicMessageType : int
        {
            Say = 0,
            Shout = 1,
            Spell = 2
        }

        #endregion

        public enum LogType : int
        {
            General = 0,
            Scripting = 1,
            GmActivity = 2,
            UserActivity = 3,
            Spawn = 4
        }

        public enum UserStatus : byte
        {
            Awake = 0,
            DoNotDisturb = 1,
            DayDreaming = 2,
            NeedGroup = 3,
            Grouped = 4,
            LoneHunter = 5,
            GroupHunter = 6,
            NeedHelp = 7
        }

        #region Slots, element types, item types

        public enum ItemSlots : int
        {
            None = 0,
            Weapon = 1,
            Armor = 2,
            Shield = 3,
            Helmet = 4,
            Earring = 5,
            Necklace = 6,
            LHand = 7,
            RHand = 8,
            LArm = 9,
            RArm = 10,
            Waist = 11,
            Leg = 12,
            Foot = 13,
            // The rest are all "vanity" slots
            FirstAcc = 14,
            Trousers = 15,
            Coat = 16,
            SecondAcc = 17,
            ThirdAcc = 18
        }

        public static class ServerItemSlots
        {
            public const int Weapon = 0;
            public const int Armor = 1;
            public const int Shield = 2;
            public const int Helmet = 3;
            public const int Earring = 4;
            public const int Necklace = 5;
            public const int LHand = 6;
            public const int RHand = 7;
            public const int LArm = 8;
            public const int RArm = 9;
            public const int Waist = 10;
            public const int Leg = 11;
            public const int Foot = 12;
            public const int FirstAcc = 13;
            public const int Trousers = 14;
            public const int Coat = 15;
            public const int SecondAcc = 16;
            public const int ThirdAcc = 17;
            // These are special edge cases; the slots don't actually exist
            public const int Gauntlet = 19;
            public const int Ring = 20;
        }

        public static class ClientItemSlots
        {
            public const int None = 0;
            public const int Weapon = 1;
            public const int Armor = 2;
            public const int Shield = 3;
            public const int Helmet = 4;
            public const int Earring = 5;
            public const int Necklace = 6;
            public const int LHand = 7;
            public const int RHand = 8;
            public const int LArm = 9;
            public const int RArm = 10;
            public const int Waist = 11;
            public const int Leg = 12;
            public const int Foot = 13;
            public const int FirstAcc = 14;
            public const int Trousers = 15;
            public const int Coat = 16;
            public const int SecondAcc = 17;
            public const int ThirdAcc = 18;
            // These are special edge cases; the slots don't actually exist
            public const int Gauntlet = 19;
            public const int Ring = 20;

        }

        public enum ItemObjectType
        {
            CanUse,
            CannotUse,
            Equipment
        }

        public enum WeaponObjectType
        {
            None,
            Basic,
            TwoHanded,
            Dagger,
            Staff,
            Claw
        }

        public enum ItemDropType
        {
            Normal,
            UserDeathPile,
            MonsterLootPile            
        }

        #endregion

        [Flags]
        public enum PlayerFlags : int
        {
            Alive = 1,
            InExchange = 2,
            InDialog = 4,
            Casting = 8,
            Pvp = 16,
            InBoard = 32,
            AliveExchange = (Alive | InExchange),
            ProhibitCast = (InExchange | InDialog | Casting)
        }

        public enum CleanupType : int
        {
            ByConnectionId = 0,
            ByName = 1
        }

        [Flags]
        public enum StatUpdateFlags : byte
        {
            UnreadMail = 0x01,
            Unknown = 0x02,
            Secondary = 0x04,
            Experience = 0x08,
            Current = 0x10,
            Primary = 0x20,
            GameMasterA = 0x40,
            GameMasterB = 0x80,
            Swimming = (GameMasterA | GameMasterB),
            Stats = (Primary | Current | Secondary),
            Full = (Primary | Current | Experience | Secondary | GameMasterA | GameMasterB)
        }

        public enum ThrottleResult : int
        {
            OK = 0,
            Throttled = 1,
            Squelched = 2,
            Disconnect = 3,
            ThrottleEnd = 4,
            SquelchEnd = 5,
            Error = 255
        }

        public enum MonsterType
        {
            Normal,
            Nonsolid,
            Merchant,
            Guardian,
            Reactor
        }

        public class EnumUtil
        {
            public static T ParseEnum<T>(string value, T defaultValue) where T : struct, IConvertible
            {
                if (!typeof(T).IsEnum) throw new ArgumentException("T must be an enumerated type");
                if (string.IsNullOrEmpty(value)) return defaultValue;

                foreach (T item in Enum.GetValues(typeof(T)))
                {
                    if (item.ToString().ToLower().Equals(value.Trim().ToLower())) return item;
                }
                return defaultValue;
            }
        }
    }

}
