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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using C3;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.XSD;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using Community.CsharpSqlite;

namespace Hybrasyl.Objects
{
    [JsonObject(MemberSerialization.OptIn)]
    public class WorldObject : IQuadStorable
    {
        public static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The rectangle that defines the object's boundaries.
        /// </summary>
        public Rectangle Rect => new Rectangle(X, Y, 1, 1);

        public bool HasMoved { get; set; }

        public byte X { get; set; }
        public byte Y { get; set; }
        public uint Id { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        public Script Script { get; set; }
        public World World { get; set; }

        public WorldObject()
        {
            Name = string.Empty;
            ResetPursuits();
        }

        public virtual void SendId()
        {
        }

        public void ResetPursuits()
        {
            Pursuits = new List<DialogSequence>();
            DialogSequences = new List<DialogSequence>();
            SequenceCatalog = new Dictionary<string, DialogSequence>();
        }

        public virtual void AddPursuit(DialogSequence pursuit)
        {
            if (pursuit.Id == null)
            {
                // This is a local sequence, so assign it into the pursuit range and
                // assign an ID
                pursuit.Id = (uint)(Constants.DIALOG_SEQUENCE_SHARED + Pursuits.Count);
                Pursuits.Add(pursuit);
            }
            else
            {
                // This is a shared sequence
                Pursuits.Add(pursuit);
            }

            if (SequenceCatalog.ContainsKey(pursuit.Name))
            {
                Logger.WarnFormat("Pursuit {0} is being overwritten", pursuit.Name);
                SequenceCatalog.Remove(pursuit.Name);

            }

            SequenceCatalog.Add(pursuit.Name, pursuit);

            if (pursuit.Id > Constants.DIALOG_SEQUENCE_SHARED)
            {
                pursuit.AssociateSequence(this);
            }
        }

        public virtual void RegisterDialogSequence(DialogSequence sequence)
        {
            sequence.Id = (uint)(Constants.DIALOG_SEQUENCE_PURSUITS + DialogSequences.Count);
            DialogSequences.Add(sequence);
            if (SequenceCatalog.ContainsKey(sequence.Name))
            {
                Logger.WarnFormat("Dialog sequence {0} is being overwritten", sequence.Name);
                SequenceCatalog.Remove(sequence.Name);

            }
            SequenceCatalog.Add(sequence.Name, sequence);
        }

        public List<DialogSequence> Pursuits;
        public List<DialogSequence> DialogSequences;
        public Dictionary<String, DialogSequence> SequenceCatalog;
    }

    public class VisibleObject : WorldObject
    {
        public new static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Map Map { get; set; }
        public Direction Direction { get; set; }
        public ushort Sprite { get; set; }
        public String Portrait { get; set; }
        public string DisplayText { get; set; }


        public VisibleObject()
        {
          
            DisplayText = String.Empty;
        }

        public virtual void AoiEntry(VisibleObject obj)
        {
        }

        public virtual void AoiDeparture(VisibleObject obj)
        {
        }

        public virtual void OnClick(User invoker)
        {
        }

        public Rectangle GetBoundingBox()
        {
            return new Rectangle(X, Y, 1, 1);
        }

        public Rectangle GetViewport()
        {
            return new Rectangle((X - Constants.VIEWPORT_SIZE / 2),
                (Y - Constants.VIEWPORT_SIZE / 2), Constants.VIEWPORT_SIZE,
                Constants.VIEWPORT_SIZE);
        }

        public Rectangle GetShoutViewport()
        {
            return new Rectangle((X - Constants.VIEWPORT_SIZE),
                (Y - Constants.VIEWPORT_SIZE), Constants.VIEWPORT_SIZE * 2,
                Constants.VIEWPORT_SIZE * 2);
        }

        public virtual void Show()
        {
            var withinViewport = Map.EntityTree.GetObjects(GetViewport());
            Logger.DebugFormat("WithinViewport contains {0} objects", withinViewport.Count);

            foreach (var obj in withinViewport)
            {
                Logger.DebugFormat("Object type is {0} and its name is {1}", obj.GetType(), obj.Name);
                obj.AoiEntry(this);
            }
        }

        public virtual void ShowTo(VisibleObject obj)
        {
        }

        public virtual void Hide()
        {
        }

        public virtual void HideFrom(VisibleObject obj)
        {
        }

        public virtual void Remove()
        {
            Map.Remove(this);
        }

        public virtual void Teleport(ushort mapid, byte x, byte y)
        {
            if (!World.Maps.ContainsKey(mapid)) return;
            Map?.Remove(this);
            Logger.DebugFormat("Teleporting {0} to {1}.", Name, World.Maps[mapid].Name);
            World.Maps[mapid].Insert(this, x, y);
        }

        public virtual void Teleport(string name, byte x, byte y)
        {
            Map targetMap;
            if (!World.MapCatalog.TryGetValue(name, out targetMap)) return;
            Map?.Remove(this);
            Logger.DebugFormat("Teleporting {0} to {1}.", Name, targetMap.Name);
            targetMap.Insert(this, x, y);
        }

        public virtual void SendMapInfo()
        {
        }

        public virtual void SendLocation()
        {
        }

        public virtual int Distance(VisibleObject obj)
        {
            return Point.Distance(obj.X, obj.Y, X, Y);
        }

        public virtual void Say(string message)
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    var x0D = new ServerPacket(0x0D);
                    x0D.WriteByte(0x00);
                    x0D.WriteUInt32(Id);
                    x0D.WriteString8($"{Name}: {message}");
                    user.Enqueue(x0D);
                }
            }
        }

        public virtual void Shout(string message)
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetShoutViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    var x0D = new ServerPacket(0x0D);
                    x0D.WriteByte(0x01);
                    x0D.WriteUInt32(Id);
                    x0D.WriteString8($"{Name}! {message}");

                    user.Enqueue(x0D);
                }
            }
        }

        public virtual void Effect(short x, short y, ushort effect, short speed)
        {
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>().Select(obj => obj))
            {
                user.SendEffect(x, y, effect, speed);
            }
        }

        public virtual void Effect(ushort effect, short speed)
        {
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>().Select(obj => obj))
            {
                user.SendEffect(Id, effect, speed);
            }
        }

        public virtual void PlaySound(ServerPacket packet)
        {
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>().Select(obj => obj))
            {
                var nPacket = (ServerPacket)packet.Clone();
                user.Enqueue(nPacket);
            }
        }

        public void DisplayPursuits(User invoker)
        {
            var menupacket = new ServerPacket(0x2F);
            // menuType (0), objectType (1 for "creature"), objectID, random, sprite, spritecolor,
            // random1 (same as random), sprite, spriteColor, ??, promptName (Green "nameplate" text on dialog),
            // byte pursuitsCount, array <string pursuitName, uint16 pursuitID>
            menupacket.WriteByte(0);

            if (this is Merchant || this is Creature)
            {
                menupacket.WriteByte(1);
            }
            else if (this is Item)
            {
                menupacket.WriteByte(2);
            }
            else if (this is Reactor)
            {
                menupacket.WriteByte(4);
            }
            else
            {
                menupacket.WriteByte(3); // this is probably bad
            }

            menupacket.WriteUInt32(Id);
            menupacket.WriteByte(1);
            menupacket.WriteUInt16((ushort)(0x4000 + Sprite));
            menupacket.WriteByte(0);
            menupacket.WriteByte(1);
            menupacket.WriteUInt16((ushort)(0x4000 + Sprite));
            menupacket.WriteByte(0);
            menupacket.WriteByte(0);
            menupacket.WriteString8(Name);
            menupacket.WriteString16(DisplayText ?? string.Empty);

            // Generate our list of dialog options
            var countPosition = menupacket.Position;
            menupacket.WriteByte(0);

            var pursuitCount = Pursuits.Count;

            var merchant = this as Merchant;
            if (merchant?.Jobs.HasFlag(MerchantJob.Vend) ?? false)
            {
                menupacket.WriteString8("Buy");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.BuyItemMenu);
                menupacket.WriteString8("Sell");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.SellItemMenu);
                pursuitCount += 2;
            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Bank) ?? false)
            {
                menupacket.WriteString8("Withdraw Item");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.WithdrawItemMenu);
                menupacket.WriteString8("Withdraw Gold");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.WithdrawGoldMenu);
                menupacket.WriteString8("Deposit Item");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.DepositItemMenu);
                menupacket.WriteString8("Deposit Gold");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.DepositGoldMenu);
                pursuitCount += 4;
            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Repair) ?? false)
            {
                menupacket.WriteString8("Repair Item");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.RepairItemMenu);
                menupacket.WriteString8("Repair All Items");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.RepairAllItems);
                pursuitCount += 2;
            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Train) ?? false)
            {
                /* if merchant has skills available to user:
                     *     menupacket.WriteString8("Learn Skill");
                     *     menupacket.WriteUInt16((ushort)MerchantMenuItem.LearnSkillMenu);
                     *     pursuitCount++;
                     * if merchant has spells available to user:
                     *     menupacket.WriteString8("Learn Spell");
                     *     menupacket.WriteUInt16((ushort)MerchantMenuItem.LearnSpellMenu);
                     *     pursuitCount++;
                     */
                menupacket.WriteString8("Forget Skill");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.ForgetSkillMenu);
                menupacket.WriteString8("Forget Spell");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.ForgetSpellMenu);
                pursuitCount += 2;
            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Post) ?? false)
            {
                menupacket.WriteString8("Send Parcel");
                menupacket.WriteUInt16((ushort)MerchantMenuItem.SendParcelMenu);
                pursuitCount++;
                /* if user has item named "Letter"
                     *     menupacket.WriteString8("Send Letter");
                     *     menupacket.WriteUInt16((ushort)MerchantMenuItem.SendLetterMenu);
                     *     pursuitCount++;
                     * if user has incoming parcel
                     *     menupacket.WriteString8("Receive Parcel");
                     *     menupacket.WriteUInt16((ushort)MerchantMenuItem.ReceiveParcel);
                     *     pursuitCount++;
                     */
            }

            foreach (var pursuit in Pursuits)
            {
                Logger.DebugFormat("Pursuit {0}, id {1}", pursuit.Name, pursuit.Id);
                menupacket.WriteString8(pursuit.Name);
                if (pursuit.Id != null) menupacket.WriteUInt16((ushort)pursuit.Id);
            }

            menupacket.Seek(countPosition, PacketSeekOrigin.Begin);
            menupacket.WriteByte((byte)pursuitCount);

            menupacket.DumpPacket();
            invoker.Enqueue(menupacket);
        }
    }

    /// <summary>
    /// Due to Door's refusal to not suck, it needs to be stuck in the quadtree.
    /// So here it is as a VisibleObject subclass. It needs to be rewritten to use the
    /// Merchant / Signpost onClick way of doing things.
    /// </summary>
    public class Door : VisibleObject
    {
        public new static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool Closed { get; set; }
        public bool IsLeftRight { get; set; }
        public bool UpdateCollision { get; set; }

        public Door(byte x, byte y, bool closed = false, bool isLeftRight = false, bool updateCollision = true)
        {
            X = x;
            Y = y;
            Closed = closed;
            IsLeftRight = isLeftRight;
            UpdateCollision = updateCollision;
        }

        public override void OnClick(User invoker)
        {
            invoker.Map.ToggleDoors(X, Y);
        }

        public override void AoiEntry(VisibleObject obj)
        {
            ShowTo(obj);
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (!(obj is User)) return;
            var user = (User)obj;
            user.SendDoorUpdate(X, Y, Closed,
                IsLeftRight);
        }
    }

    public class Creature : VisibleObject
    {
        public new static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [JsonProperty]
        public byte Level { get; set; }

        [JsonProperty]
        public uint Experience { get; set; }

        [JsonProperty]
        public byte Ability { get; set; }

        [JsonProperty]
        public uint AbilityExp { get; set; }

        [JsonProperty]
        public uint Hp { get; set; }

        [JsonProperty]
        public uint Mp { get; set; }

        [JsonProperty]
        public long BaseHp { get; set; }

        [JsonProperty]
        public long BaseMp { get; set; }

        [JsonProperty]
        public long BaseStr { get; set; }

        [JsonProperty]
        public long BaseInt { get; set; }

        [JsonProperty]
        public long BaseWis { get; set; }

        [JsonProperty]
        public long BaseCon { get; set; }

        [JsonProperty]
        public long BaseDex { get; set; }

        public long BonusHp { get; set; }
        public long BonusMp { get; set; }
        public long BonusStr { get; set; }
        public long BonusInt { get; set; }
        public long BonusWis { get; set; }
        public long BonusCon { get; set; }
        public long BonusDex { get; set; }
        public long BonusDmg { get; set; }
        public long BonusHit { get; set; }
        public long BonusAc { get; set; }
        public long BonusMr { get; set; }
        public long BonusRegen { get; set; }

        public Enums.Element OffensiveElement { get; set; }
        public Enums.Element DefensiveElement { get; set; }

        public ushort MapId { get; protected set; }
        public byte MapX { get; protected set; }
        public byte MapY { get; protected set; }

        [JsonProperty]
        public uint Gold { get; set; }

        [JsonProperty]
        public Inventory Inventory { get; protected set; }

        [JsonProperty("Equipment")]
        public Inventory Equipment { get; protected set; }

        public Creature()
        {
            Gold = 0;
            Inventory = new Inventory(59);
            Equipment = new Inventory(18);
        }

        public override void OnClick(User invoker)
        {
        }

        // Restrict to (inclusive) range between [min, max]. Max is optional, and if its
        // not present then no upper limit will be enforced.
        private static long BindToRange(long start, long? min, long? max)
        {
            if (min != null && start < min)
                return min.GetValueOrDefault();
            else if (max != null && start > max)
                return max.GetValueOrDefault();
            else
                return start;
        }

        public uint MaximumHp
        {
            get
            {
                var value = BaseHp + BonusHp;

                if (value > uint.MaxValue)
                    return uint.MaxValue;

                if (value < uint.MinValue)
                    return uint.MinValue;

                return (uint)BindToRange(value, StatLimitConstants.MIN_BASE_HPMP, StatLimitConstants.MAX_BASE_HPMP);
            }
        }

        public uint MaximumMp
        {
            get
            {
                var value = BaseMp + BonusMp;

                if (value > uint.MaxValue)
                    return uint.MaxValue;

                if (value < uint.MinValue)
                    return uint.MinValue;

                return (uint)BindToRange(value, StatLimitConstants.MIN_BASE_HPMP, StatLimitConstants.MAX_BASE_HPMP);
            }
        }

        public byte Str
        {
            get
            {
                var value = BaseStr + BonusStr;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Int
        {
            get
            {
                var value = BaseInt + BonusInt;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Wis
        {
            get
            {
                var value = BaseWis + BonusWis;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Con
        {
            get
            {
                var value = BaseCon + BonusCon;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Dex
        {
            get
            {
                var value = BaseDex + BonusDex;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Dmg
        {
            get
            {
                if (BonusDmg > byte.MaxValue)
                    return byte.MaxValue;

                if (BonusDmg < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(BonusDmg, StatLimitConstants.MIN_DMG, StatLimitConstants.MAX_DMG);
            }
        }

        public byte Hit
        {
            get
            {
                if (BonusHit > byte.MaxValue)
                    return byte.MaxValue;

                if (BonusHit < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(BonusHit, StatLimitConstants.MIN_HIT, StatLimitConstants.MAX_HIT);
            }
        }

        public sbyte Ac
        {
            get
            {
                Logger.DebugFormat("BonusAc is {0}", BonusAc);
                var value = 100 - Level / 3 + BonusAc;

                if (value > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (value < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BindToRange(value, StatLimitConstants.MIN_AC, StatLimitConstants.MAX_AC);
            }
        }

        public sbyte Mr
        {
            get
            {
                if (BonusMr > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (BonusMr < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BindToRange(BonusMr, StatLimitConstants.MIN_MR, StatLimitConstants.MAX_MR);
            }
        }

        public sbyte Regen
        {
            get
            {
                if (BonusRegen > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (BonusRegen < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BonusRegen;
            }
        }

        private uint _mLastHitter;

        public Creature LastHitter
        {
            get
            {
                return World.Objects.ContainsKey(_mLastHitter) ? (Creature)World.Objects[_mLastHitter] : null;
            }
            set
            {
                _mLastHitter = value?.Id ?? 0;
            }
        }

        public bool AbsoluteImmortal { get; set; }
        public bool PhysicalImmortal { get; set; }
        public bool MagicalImmortal { get; set; }

        public virtual void Attack(Direction direction, Castable castObject, Creature target = null)
        {
            //do something?
        }

        public virtual void Attack(Castable castObject, Creature target)
        {
            //do spell?
        }

        public virtual void Attack(Castable castObject)
        {
            //do aoe?
        }

        public void SendAnimation(ServerPacket packet)
        {
            Logger.InfoFormat("SendAnimation");
            Logger.InfoFormat("SendAnimation byte format is: {0}", BitConverter.ToString(packet.ToArray()));
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)packet.Clone();
                Logger.InfoFormat("SendAnimation to {0}",user.Name);
                user.Enqueue(nPacket);
                
            }
        }

        public virtual void UpdateAttributes(StatUpdateFlags flags)
        {
        }

        public virtual bool Walk(Direction direction)
        {
            return false;
        }

        public virtual bool Turn(Direction direction)
        {
            Direction = direction;

            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (!(obj is User)) continue;
                var user = obj as User;
                var x11 = new ServerPacket(0x11);
                x11.WriteUInt32(Id);
                x11.WriteByte((byte)direction);
                user.Enqueue(x11);
            }

            return true;
        }

        public virtual void Motion(byte motion, short speed)
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    user.SendMotion(Id, motion, speed);
                }
            }
        }

        //public virtual bool AddItem(Item item, bool updateWeight = true) { return false; }
        //public virtual bool AddItem(Item item, int slot, bool updateWeight = true) { return false; }
        //public virtual bool RemoveItem(int slot, bool updateWeight = true) { return false; }
        //public virtual bool RemoveItem(int slot, int count, bool updateWeight = true) { return false; }
        //public virtual bool AddEquipment(Item item) { return false; }
        //public virtual bool AddEquipment(Item item, byte slot, bool sendUpdate = true) { return false; }
        //public virtual bool RemoveEquipment(byte slot) { return false; }

        public virtual void Heal(double heal, Creature healer = null)
        {
            if (AbsoluteImmortal || PhysicalImmortal)
                return;

            if (Hp == MaximumHp || heal > MaximumHp)
                return;

            Hp = heal > uint.MaxValue ? MaximumHp : Math.Min(MaximumHp, (uint)(Hp + heal));

            SendDamageUpdate(this);

        }

        public virtual void RegenerateMp(double mp, Creature regenerator = null)
        {
            if (AbsoluteImmortal)
                return;

            if (Mp == MaximumMp || mp > MaximumMp)
                return;

            Mp = mp > uint.MaxValue ? MaximumMp : Math.Min(MaximumMp, (uint) (Mp + mp));
        }

        public virtual void Damage(double damage, Enums.Element element = Enums.Element.None, Enums.DamageType damageType = Enums.DamageType.Direct, Creature attacker = null)
        {
            if (damageType == Enums.DamageType.Physical && (AbsoluteImmortal || PhysicalImmortal))
                return;

            if (damageType == Enums.DamageType.Magical && (AbsoluteImmortal || MagicalImmortal))
                return;

            if (damageType != Enums.DamageType.Direct)
            {
                double armor = Ac * -1 + 100;
                var resist = Game.ElementTable[(int)element, 0];
                var reduction = damage * (armor / (armor + 50));
                damage = (damage - reduction) * resist;
            }

            if (attacker == null)
                _mLastHitter = attacker.Id;

            var normalized = (uint)damage;

            if (normalized > Hp)
                normalized = Hp;

            Hp -= normalized;

            SendDamageUpdate(this);
        }

        private void SendDamageUpdate(Creature creature)
        {
            var percent = ((creature.Hp / (double)creature.MaximumHp) * 100);
            var healthbar = new ServerPacketStructures.HealthBar() {CurrentPercent = (byte)percent, ObjId = creature.Id};

            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)healthbar.Packet().Clone();
                user.Enqueue(nPacket);
            }
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (!(obj is User)) return;
            var user = (User)obj;
            user.SendVisibleCreature(this);
        }

        public virtual void Refresh()
        {
        }
    }

    public class Monster : Creature, ICloneable
    {
        private bool _idle = true;

        private uint _mTarget;

        public Creature Target
        {
            get
            {
                return World.Objects.ContainsKey(_mTarget) ? (Creature)World.Objects[_mTarget] : null;
            }
            set
            {
                _mTarget = value?.Id ?? 0;
            }
        }

        public override int GetHashCode()
        {
            return (Name.GetHashCode() * Id.GetHashCode()) - 1;
        }

        public virtual bool Pathfind(byte x, byte y)
        {
            var xDelta = Math.Abs(x - X);
            var yDelta = Math.Abs(y - Y);

            if (xDelta > yDelta)
            {
                Walk(x > X ? Direction.East : Direction.West);
            }

            return false;
        }

        public override void Attack(Direction direction, Castable castObject, Creature target)
        {
            //do monster attack.
        }

        public override void Attack(Castable castObject, Creature target)
        {
            //do monster spell
        }

        public override void Attack(Castable castObject)
        {
            //do monster aoe
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                user.SendVisibleCreature(this);
            }
        }

        public bool IsIdle()
        {
            return _idle;
        }

        public void Awaken()
        {
            _idle = false;
            //add to alive monsters?
        }

        public void Sleep()
        {
            _idle = true;
            //return to idle state
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    public class Signpost : VisibleObject
    {
        public string Message { get; set; }
        public bool IsMessageboard { get; set; }
        public string BoardName { get; set; }

        public Signpost(byte postX, byte postY, string message, bool messageboard = false,
            string boardname = null)
            : base()
        {
            X = postX;
            Y = postY;
            Message = message;
            IsMessageboard = messageboard;
            BoardName = boardname;
        }

        public override void OnClick(User invoker)
        {
            Logger.DebugFormat("Signpost was clicked");
            if (!IsMessageboard)
            {
                invoker.SendMessage(Message, Message.Length < 1024 ? (byte)MessageTypes.SLATE : (byte)MessageTypes.SLATE_WITH_SCROLLBAR);
            }
            else
            {
                var board = World.GetBoard(BoardName);
                invoker.Enqueue(board.RenderToPacket(true));
            }
        }
    }

    public class Gold : VisibleObject
    {
        public uint Amount { get; set; }

        public new string Name
        {
            get
            {
                if (Amount == 1) return "Silver Coin";
                else if (Amount < 100) return "Gold Coin";
                else if (Amount < 1000) return "Silver Pile";
                else return "Gold Pile";
            }
        }

        public new ushort Sprite
        {
            get
            {
                if (Amount == 1) return 138;
                else if (Amount < 100) return 137;
                else if (Amount < 1000) return 141;
                else return 140;
            }
        }

        public Gold(uint amount)
        {
            Amount = amount;
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (!(obj is User)) return;
            var user = (User)obj;
            user.SendVisibleGold(this);
        }
    }

    public class Reactor : VisibleObject
    {
        //private reactor _reactor;
        private HybrasylWorldObject _world;

        public bool Ready;

        public Reactor(/* reactor reactor*/)
        {
            /*
            _reactor = reactor;
            _world = new HybrasylWorldObject(this);
            X = (byte)_reactor.map_x;
            Y = (byte)_reactor.map_y;
            Ready = false;
            Script = null;
             */
        }

        public void OnSpawn()
        {
            // Do we have a script?
            /*
                        Script thescript;
                        if (_reactor.script_name == String.Empty)
                            Game.World.ScriptProcessor.TryGetScript(_reactor.name, out thescript);
                        else
                            Game.World.ScriptProcessor.TryGetScript(_reactor.script_name, out thescript);

                        if (thescript == null)
                        {
                            Logger.WarnFormat("reactor {0}: script not found", _reactor.name);
                            return;
                        }

                        Script = thescript;

                        Script.AssociateScriptWithObject(this);

                        if (!Script.InstantiateScriptable())
                        {
                            Logger.WarnFormat("reactor {0}: script instantiation failed", _reactor.name);
                            return;
                        }

                        Script.ExecuteScriptableFunction("OnSpawn");
                        Ready = true;
             */
        }

        public void OnEntry(WorldObject obj)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnEntry", Script.GetObjectWrapper(obj));
        }

        public void AoiEntry(WorldObject obj)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnAoiEntry", Script.GetObjectWrapper(obj));
        }

        public void OnLeave(WorldObject obj)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnLeave", Script.GetObjectWrapper(obj));
        }

        public void AoiDeparture(WorldObject obj)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnAoiDeparture", Script.GetObjectWrapper(obj));
        }

        public void OnDrop(WorldObject obj, WorldObject dropped)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnDrop", Script.GetObjectWrapper(obj),
                    Script.GetObjectWrapper(dropped));
        }
    }
}