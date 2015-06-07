using C3;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using log4net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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
        public String Portrait { get; set; }
        public string DisplayText { get; set; }

        public List<DialogSequence> Pursuits;
        public List<DialogSequence> DialogSequences;
        public Dictionary<String, DialogSequence> SequenceCatalog;

        public VisibleObject()
        {
            Pursuits = new List<DialogSequence>();
            DialogSequences = new List<DialogSequence>();
            SequenceCatalog = new Dictionary<String, DialogSequence>();
            DisplayText = String.Empty;
        }

        public virtual void AoiEntry(VisibleObject obj)
        {
        }

        public virtual void AddPursuit(DialogSequence pursuit)
        {
            if (pursuit.Id == null)
            {
                pursuit.Id = (uint)(Constants.DIALOG_SEQUENCE_SHARED + Pursuits.Count());
                Pursuits.Add(pursuit);
            }
            else
            {
            }

            if (SequenceCatalog.ContainsKey(pursuit.Name))
            {
                SequenceCatalog.Remove(pursuit.Name);
                SequenceCatalog.Add(pursuit.Name, pursuit);
            }
            else
            {
                SequenceCatalog.Add(pursuit.Name, pursuit);
            }

            if (pursuit.Id > Constants.DIALOG_SEQUENCE_SHARED)
            {
                pursuit.AssociateSequence(this);
            }
        }

        public virtual void RegisterDialogSequence(DialogSequence sequence)
        {
            sequence.Id = (uint)(Constants.DIALOG_SEQUENCE_PURSUITS + DialogSequences.Count());
            DialogSequences.Add(sequence);
            SequenceCatalog.Add(sequence.Name, sequence);
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
            if (World.Maps.ContainsKey(mapid))
            {
                if (Map != null)
                {
                    Map.Remove(this);
                }
                Logger.DebugFormat("Teleporting {0} to {1}.", Name, World.Maps[mapid].Name);
                World.Maps[mapid].Insert(this, x, y);
            }
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
                    x0D.WriteString8(string.Format("{0}: {1}", Name, message));
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
                    x0D.WriteString8(string.Format("{0}! {1}", Name, message));

                    user.Enqueue(x0D);
                }
            }
        }

        public virtual void Effect(short x, short y, ushort effect, short speed)
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    user.SendEffect(x, y, effect, speed);
                }
            }
        }

        public virtual void Effect(ushort effect, short speed)
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    user.SendEffect(Id, effect, speed);
                }
            }
        }

        public virtual void PlaySound(byte sound)
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    user.SendSound(sound);
                }
            }
        }

        public void DisplayPursuits(User invoker)
        {
            var menupacket = new ServerPacket(0x2F);
            menupacket.WriteByte(0);

            if (this is Merchant || this is Creature)
            {
                menupacket.WriteByte(1);
            }
            else
            {
                if (this is Item)
                {
                    menupacket.WriteByte(2);
                }
                else
                {
                    menupacket.WriteByte(3);
                }
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
            menupacket.WriteString16(DisplayText ?? String.Empty);

            var countPosition = menupacket.Position;
            menupacket.WriteByte(0);

            var pursuitCount = Pursuits.Count;

            if (this is Merchant)
            {
                var merchant = (Merchant)this;
                if (merchant.Jobs.HasFlag(MerchantJob.Vendor))
                {
                    menupacket.WriteString8("Buy");
                    menupacket.WriteUInt16((ushort)MerchantMenuItem.BuyItemMenu);
                    menupacket.WriteString8("Sell");
                    menupacket.WriteUInt16((ushort)MerchantMenuItem.SellItemMenu);
                    pursuitCount += 2;
                }
                if (merchant.Jobs.HasFlag(MerchantJob.Banker))
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
                if (merchant.Jobs.HasFlag(MerchantJob.Repairer))
                {
                    menupacket.WriteString8("Repair Item");
                    menupacket.WriteUInt16((ushort)MerchantMenuItem.RepairItemMenu);
                    menupacket.WriteString8("Repair All Items");
                    menupacket.WriteUInt16((ushort)MerchantMenuItem.RepairAllItems);
                    pursuitCount += 2;
                }
                if (merchant.Jobs.HasFlag(MerchantJob.Trainer))
                {
                    menupacket.WriteString8("Forget Skill");
                    menupacket.WriteUInt16((ushort)MerchantMenuItem.ForgetSkillMenu);
                    menupacket.WriteString8("Forget Spell");
                    menupacket.WriteUInt16((ushort)MerchantMenuItem.ForgetSpellMenu);
                    pursuitCount += 2;
                }
                if (merchant.Jobs.HasFlag(MerchantJob.Postman))
                {
                    menupacket.WriteString8("Send Parcel");
                    menupacket.WriteUInt16((ushort)MerchantMenuItem.SendParcelMenu);
                    pursuitCount++;
                }
            }

            foreach (var pursuit in Pursuits)
            {
                Logger.DebugFormat("Pursuit {0}, id {1}", pursuit.Name, pursuit.Id);
                menupacket.WriteString8(pursuit.Name);
                menupacket.WriteUInt16((ushort)pursuit.Id);
            }

            menupacket.Seek(countPosition, PacketSeekOrigin.Begin);
            menupacket.WriteByte((byte)pursuitCount);

            invoker.Enqueue(menupacket);
        }
    }
}
