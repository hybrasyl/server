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
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Hybrasyl.Enums;
using log4net;

namespace Hybrasyl.Objects
{
    public class VisibleObject : WorldObject
    {
        public new static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Map Map { get; set; }
        public Direction Direction { get; set; }
        public ushort Sprite { get; set; }
        public string Portrait { get; set; }
        public string DisplayText { get; set; }

        public string DeathPileOwner { get; set; }
        public List<string> DeathPileAllowedLooters { get; set; }
        public DateTime? DeathPileTime { get; set; }

        public VisibleObject()
        {
            DisplayText = string.Empty;
            DeathPileAllowedLooters = new List<string>();
            DeathPileOwner = string.Empty;
            DeathPileTime = null;
        }

        public virtual void AoiEntry(VisibleObject obj)
        {
        }

        public virtual void AoiDeparture(VisibleObject obj)
        {
        }

        public bool CanBeLooted(string username, out string error)
        {
            error = string.Empty;
            // Let's just be sure here
            if (!(this is Gold || this is ItemObject))
            {
                error = "You can't do that.";
                return false;
            }
            // Check if the item is part of a death pile
            if (DeathPileTime == null) return true;
            if (DeathPileOwner == username) return true;
            if (DeathPileAllowedLooters.Contains(username) &&
                (DateTime.Now - DeathPileTime.Value).Seconds > Constants.DEATHPILE_GROUP_TIMEOUT) return true;
            if ((DateTime.Now - DeathPileTime.Value).Seconds > Constants.DEATHPILE_RANDO_TIMEOUT) return true;
            error = "These items are cursed.";
            return false;
        }

        public virtual void OnClick(User invoker)
        {
        }

        public virtual void OnDeath() { }

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
            if (!World.WorldData.ContainsKey<Map>(mapid)) return;
            Map?.Remove(this);
            Logger.DebugFormat("Teleporting {0} to {1}.", Name, World.WorldData.Get<Map>(mapid).Name);
            World.WorldData.Get<Map>(mapid).Insert(this, x, y);
        }

        public virtual void Teleport(string name, byte x, byte y)
        {
            Map targetMap;
            if (!World.WorldData.TryGetValueByIndex(name, out targetMap)) return;
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
            var greeting = World.Strings.Merchant.FirstOrDefault(x => x.Key == "greeting");
            var optionsCount = 0;
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var merchant = this as Merchant;
            if (merchant?.Jobs.HasFlag(MerchantJob.Vend) ?? false)
            {
                optionsCount += 2;
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.BuyItemMenu, Text = "Buy"});
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.SellItemMenu, Text = "Sell" });
            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Bank) ?? false)
            {
                optionsCount += 4;
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.DepositGoldMenu, Text = "Deposit Gold" });
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.WithdrawGoldMenu, Text = "Withdraw Gold" });
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.DepositItemMenu, Text = "Deposit Item" });
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.WithdrawItemMenu, Text = "Withdraw Item" });
            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Repair) ?? false)
            {
                optionsCount += 2;
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.RepairItemMenu, Text = "Fix Item" });
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.RepairAllItems, Text = "Fix All Items" });
                
            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Skills) ?? false)
            {
                optionsCount += 2;
                options.Options.Add(new MerchantDialogOption { Id = (ushort) MerchantMenuItem.LearnSkillMenu, Text = "Learn Skill" });
                options.Options.Add(new MerchantDialogOption { Id = (ushort) MerchantMenuItem.ForgetSkillMenu, Text = "Forget Skill" });

            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Spells) ?? false)
            {
                optionsCount += 2;
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.LearnSpellMenu, Text = "Learn Secret" });
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.ForgetSpellMenu, Text = "Forget Secret" });

            }
            if (merchant?.Jobs.HasFlag(MerchantJob.Post) ?? false)
            {
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.SendParcelMenu, Text = "Send Parcel" });
                optionsCount++;

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
                options.Options.Add(new MerchantDialogOption { Id = (ushort)pursuit.Id.Value, Text = pursuit.Name} );
                optionsCount++;

            }
            options.OptionsCount = (byte)optionsCount;
        
            var packet =new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = Id,
                Tile1 = (ushort)(0x4000 + Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = Name,
                Text = greeting?.Value ?? string.Empty,
                Options = options
            };

            //var menupacket = new ServerPacket(0x2F); //opcode
            //// menuType (0), objectType (1 for "creature"), objectID, random, sprite, spritecolor,
            //// random1 (same as random), sprite, spriteColor, ??, promptName (Green "nameplate" text on dialog),
            //// byte pursuitsCount, array <string pursuitName, uint16 pursuitID>
            //menupacket.WriteByte(0); //dialogtype

            //if (this is Merchant || this is Creature) //objecttype
            //{
            //    menupacket.WriteByte(1);
            //}
            //else if (this is ItemObject)
            //{
            //    menupacket.WriteByte(2);
            //}
            //else if (this is Reactor)
            //{
            //    menupacket.WriteByte(4);
            //}
            //else
            //{
            //    menupacket.WriteByte(3); // this is probably bad
            //}

            //menupacket.WriteUInt32(Id); //objectid
            //menupacket.WriteByte(1); //unknow4
            //menupacket.WriteUInt16((ushort)(0x4000 + Sprite)); //tile1
            //menupacket.WriteByte(0); //color1
            //menupacket.WriteByte(1); //unknow7
            ////menupacket.WriteUInt16(1);
            //menupacket.WriteUInt16((ushort)(0x4000 + Sprite)); //tile2
            //menupacket.WriteByte(0); //color2
            //menupacket.WriteByte(0); //unknow10 = sprite type??
            ////menupacket.WriteUInt16(1);
            //menupacket.WriteString8(Name);
            //var firstOrDefault = World.Strings.Merchant.FirstOrDefault(x => x.Key == "greeting");
            //if (firstOrDefault != null)
            //    menupacket.WriteString16(firstOrDefault.Value ?? string.Empty);

            //// Generate our list of dialog options
            //var countPosition = menupacket.Position;
            //menupacket.WriteByte(0);

            //var pursuitCount = Pursuits.Count;

            //var merchant = this as Merchant;
            //if (merchant?.Jobs.HasFlag(MerchantJob.Vend) ?? false)
            //{
            //    menupacket.WriteString8("Buy");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.BuyItemMenu);
            //    menupacket.WriteString8("Sell");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.SellItemMenu);
            //    pursuitCount += 2;
            //}
            //if (merchant?.Jobs.HasFlag(MerchantJob.Bank) ?? false)
            //{
            //    menupacket.WriteString8("Withdraw Item");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.WithdrawItemMenu);
            //    menupacket.WriteString8("Withdraw Gold");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.WithdrawGoldMenu);
            //    menupacket.WriteString8("Deposit Item");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.DepositItemMenu);
            //    menupacket.WriteString8("Deposit Gold");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.DepositGoldMenu);
            //    pursuitCount += 4;
            //}
            //if (merchant?.Jobs.HasFlag(MerchantJob.Repair) ?? false)
            //{
            //    menupacket.WriteString8("Repair Item");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.RepairItemMenu);
            //    menupacket.WriteString8("Repair All Items");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.RepairAllItems);
            //    pursuitCount += 2;
            //}
            //if (merchant?.Jobs.HasFlag(MerchantJob.Train) ?? false)
            //{
            //    //if merchant has skills available to user:
            //    menupacket.WriteString8("Learn Skill");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.LearnSkillMenu);
            //    pursuitCount++;
            //    //if merchant has spells available to user:
            //    menupacket.WriteString8("Learn Spell");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.LearnSpellMenu);
            //    pursuitCount++;

            //    menupacket.WriteString8("Forget Skill");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.ForgetSkillMenu);
            //    menupacket.WriteString8("Forget Spell");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.ForgetSpellMenu);
            //    pursuitCount += 2;
            //}
            //if (merchant?.Jobs.HasFlag(MerchantJob.Post) ?? false)
            //{
            //    menupacket.WriteString8("Send Parcel");
            //    menupacket.WriteUInt16((ushort)MerchantMenuItem.SendParcelMenu);
            //    pursuitCount++;
            //    /* if user has item named "Letter"
            //         *     menupacket.WriteString8("Send Letter");
            //         *     menupacket.WriteUInt16((ushort)MerchantMenuItem.SendLetterMenu);
            //         *     pursuitCount++;
            //         * if user has incoming parcel
            //         *     menupacket.WriteString8("Receive Parcel");
            //         *     menupacket.WriteUInt16((ushort)MerchantMenuItem.ReceiveParcel);
            //         *     pursuitCount++;
            //         */
            //}

            //foreach (var pursuit in Pursuits)
            //{
            //    Logger.DebugFormat("Pursuit {0}, id {1}", pursuit.Name, pursuit.Id);
            //    menupacket.WriteString8(pursuit.Name);
            //    if (pursuit.Id != null) menupacket.WriteUInt16((ushort)pursuit.Id);
            //}

            //menupacket.Seek(countPosition, PacketSeekOrigin.Begin);
            //menupacket.WriteByte((byte)pursuitCount);

            //menupacket.DumpPacket();
            //invoker.Enqueue(menupacket);
            invoker.Enqueue(packet.Packet());
        }
    }
}
