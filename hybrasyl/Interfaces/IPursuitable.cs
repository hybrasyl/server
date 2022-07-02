using System.Collections.Generic;
using Hybrasyl.Dialogs;
using Hybrasyl.Objects;
using Hybrasyl.Scripting;

namespace Hybrasyl.Interfaces;

public interface IPursuitable : IInteractable, IResponseCapable, IVisible
{
    public uint Id { get; }
    List<DialogSequence> Pursuits { get; set; }

    public new string Name
    {
        get
        {
            if (this is IInteractable interactable) return interactable.Name;
            return (this as IWorldObject).Name;
        }
    }

    public void ResetPursuits()
    {
        Pursuits = new List<DialogSequence>();
        DialogSequences = new List<DialogSequence>();
        SequenceIndex = new Dictionary<string, DialogSequence>();
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

        if (SequenceIndex.ContainsKey(pursuit.Name))
        {
            GameLog.WarningFormat("Pursuit {0} is being overwritten", pursuit.Name);
            SequenceIndex.Remove(pursuit.Name);
        }

        SequenceIndex.Add(pursuit.Name, pursuit);

        if (pursuit.Id > Constants.DIALOG_SEQUENCE_SHARED)
        {
            pursuit.AssociateSequence(this as IInteractable);
        }
    }

    public sealed void DisplayPursuits(User invoker)
    {
        var optionsCount = 0;
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };
        var merchant = this as Merchant;
        if (merchant?.Jobs.HasFlag(MerchantJob.Vend) ?? false)
        {
            optionsCount += 2;
            options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.BuyItemMenu, Text = "Buy" });
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
            options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.LearnSkillMenu, Text = "Learn Skill" });
            options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.ForgetSkillMenu, Text = "Forget Skill" });

        }
        if (merchant?.Jobs.HasFlag(MerchantJob.Spells) ?? false)
        {
            optionsCount += 2;
            options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.LearnSpellMenu, Text = "Learn Secret" });
            options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.ForgetSpellMenu, Text = "Forget Secret" });

        }
        if (merchant?.Jobs.HasFlag(MerchantJob.Post) ?? false)
        {
            if (invoker.HasParcels)
            {
                options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.ReceiveParcel, Text = "Receive Parcel" });
                optionsCount++;
            }
            options.Options.Add(new MerchantDialogOption { Id = (ushort)MerchantMenuItem.SendParcelMenu, Text = "Send Parcel" });
            optionsCount++;

        }

        foreach (var pursuit in Pursuits)
        {
            GameLog.DebugFormat("Pursuit {0}, id {1}", pursuit.Name, pursuit.Id);
            if (pursuit.MenuCheckExpression != string.Empty)
            {
                var env = ScriptEnvironment.CreateWithOrigin(invoker);
                env.DialogPath = $"{Name}:DisplayPursuits:MenuCheckExpression";
                var ret = Script.ExecuteExpression(pursuit.MenuCheckExpression,
                    env);
                // If the menu check expression returns anything other than true, we don't include the 
                // pursuit on the main menu that is sent to the user
                if (!ret.Return.CastToBool())
                {
                    GameLog.ScriptingDebug($"{pursuit.MenuCheckExpression} evaluated to {ret}");
                    continue;
                }
            }

            options.Options.Add(new MerchantDialogOption { Id = (ushort)pursuit.Id.Value, Text = pursuit.Name });
            optionsCount++;

        }

        var packet = new ServerPacketStructures.MerchantResponse()
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = Id,
            Tile1 = (ushort)(0x4000 + Sprite),
            Color1 = 0,
            Tile2 = (ushort)(0x4000 + Sprite),
            Color2 = 0,
            PortraitType = 1,
            Name = Name,
            Text = merchant.GetLocalString("greeting"),
            Options = options
        };

        invoker.Enqueue(packet.Packet());
    }

}