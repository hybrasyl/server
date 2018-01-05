using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using Castable = Hybrasyl.Castables.Castable;

namespace Hybrasyl
{
    public partial class World
    {
        public void SetMerchantMenuHandlers()
        {
            merchantMenuHandlers = new Dictionary<MerchantMenuItem, MerchantMenuHandler>()
            {
                {MerchantMenuItem.MainMenu, new MerchantMenuHandler(0, MerchantMenuHandler_MainMenu)},
                {
                    MerchantMenuItem.BuyItemMenu,
                    new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItemMenu)
                },
                //{MerchantMenuItem.BuyItem, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItem)},
                {
                    MerchantMenuItem.BuyItemQuantity,
                    new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItemWithQuantity)
                },
                {
                    MerchantMenuItem.BuyItemAccept, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItemAccept)
                },
                {
                    MerchantMenuItem.SellItemMenu, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemMenu)
                },
                {
                    MerchantMenuItem.SellItem, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItem)
                },
                {
                    MerchantMenuItem.SellItemQuantity, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemWithQuantity)
                },
                {
                    MerchantMenuItem.SellItemConfirm, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemConfirmation)
                },
                {
                    MerchantMenuItem.SellItemAccept, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemAccept)
                },
                {
                    MerchantMenuItem.LearnSkillMenu, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkillMenu)
                },
                {
                    MerchantMenuItem.LearnSpellMenu, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpellMenu)
                },
                {
                    MerchantMenuItem.ForgetSkillMenu, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_ForgetSkillMenu)
                },
                {
                    MerchantMenuItem.ForgetSpellMenu, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_ForgetSpellMenu)
                },
                {
                    MerchantMenuItem.LearnSkill, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkill)
                },
                {
                    MerchantMenuItem.LearnSpell, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpell)
                },
                {
                    MerchantMenuItem.ForgetSkill, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_ForgetSkill)
                },
                {
                    MerchantMenuItem.ForgetSpell, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_ForgetSpell)
                },
                {
                    MerchantMenuItem.LearnSkillAccept, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkillAccept)
                },
                {
                    MerchantMenuItem.LearnSkillAgree, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkillAgree)
                },
                {
                    MerchantMenuItem.LearnSkillDisagree, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkillDisagree)
                },
                {
                    MerchantMenuItem.LearnSpellAccept, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpellAccept)
                },
                {
                    MerchantMenuItem.ForgetSkillAccept, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_ForgetSkillAccept)
                },
                {
                    MerchantMenuItem.ForgetSpellAccept, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_ForgetSpellAccept)
                },
                {
                    MerchantMenuItem.LearnSpellAgree, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpellAgree)
                },
                {
                    MerchantMenuItem.LearnSpellDisagree, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpellDisagree)
                },
                {
                    MerchantMenuItem.SendParcelMenu, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcelMenu)
                },
                {
                    MerchantMenuItem.SendParcelAccept, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcelAccept)
                },
                {
                    MerchantMenuItem.SendParcel, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcel)
                },
                {
                    MerchantMenuItem.SendParcelRecipient, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcelRecipient)
                },
                {
                    MerchantMenuItem.SendParcelFailure, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcelFailure)
                },

            };
        }
        #region Merchant Menu ItemObject Handlers

        private void MerchantMenuHandler_MainMenu(User user, Merchant merchant, ClientPacket packet)
        {
            merchant.DisplayPursuits(user);
        }

        private void MerchantMenuHandler_BuyItemMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowBuyMenu(merchant);
        }

        private void MerchantMenuHandler_SellItemMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowSellMenu(merchant);
        }

        private void MerchantMenuHandler_BuyItem(User user, Merchant merchant, ClientPacket packet)
        {
            //this is no longer used
            string name = packet.ReadString8();

            var template = merchant.Inventory[name];

            if (template.Stackable)
            {
                user.ShowBuyMenuQuantity(merchant, name);
                return;
            }

            if (user.Gold < template.Properties.Physical.Value)
            {
                user.ShowMerchantGoBack(merchant, "You do not have enough gold.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (user.CurrentWeight + template.Properties.Physical.Weight > user.MaximumWeight)
            {
                user.ShowMerchantGoBack(merchant, "That item is too heavy for you to carry.",
                    MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (user.Inventory.IsFull)
            {
                user.ShowMerchantGoBack(merchant, "You cannot carry any more items.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            user.RemoveGold(template.Properties.Physical.Value);
            var item = CreateItem(template.Id);
            Insert(item);
            user.AddItem(item);

            user.UpdateAttributes(StatUpdateFlags.Experience);
            user.ShowBuyMenu(merchant);
        }

        private void MerchantMenuHandler_BuyItemWithQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            string name = packet.ReadString8();
            string qStr = packet.ReadString8();

            user.ShowBuyMenuQuantity(merchant, name);
        }

        private void MerchantMenuHandler_BuyItemAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowBuyItem(merchant);
        }

        private void MerchantMenuHandler_SellItem(User user, Merchant merchant, ClientPacket packet)
        {
            byte slot = packet.ReadByte();

            var item = user.Inventory[slot];

            if (item.Stackable && item.Count > 1)
            {
                user.ShowSellQuantity(merchant, slot);
                return;
            }

            user.ShowSellConfirm(merchant, slot);
        }

        private void MerchantMenuHandler_SellItemWithQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            byte slot = packet.ReadByte();
            byte quantity = packet.ReadByte();


            if (quantity < 1)
            {
                user.ShowSellQuantity(merchant, slot);
                return;
            }

            var item = user.Inventory[slot];
            if (item == null || !item.Stackable) return;

            user.ShowSellConfirm(merchant, slot, quantity);
        }

        private void MerchantMenuHandler_SellItemConfirmation(User user, Merchant merchant, ClientPacket packet)
        {
            packet.ReadByte();
            byte slot = packet.ReadByte();
            byte quantity = packet.ReadByte();

            var item = user.Inventory[slot];
            if (item == null) return;

            if (!merchant.Inventory.ContainsKey(item.Name))
            {
                user.ShowMerchantGoBack(merchant, "I do not want that item.", MerchantMenuItem.SellItemMenu);
                return;
            }

            if (item.Count < quantity)
            {
                user.ShowMerchantGoBack(merchant, "You don't have that many to sell.", MerchantMenuItem.SellItemMenu);
                return;
            }

            uint profit = (uint)(Math.Round(item.Value * 0.50) * quantity);

            if (item.Stackable && quantity < item.Count)
                user.DecreaseItem(slot, quantity);
            else user.RemoveItem(slot);

            user.AddGold(profit);

            merchant.DisplayPursuits(user);
        }

        private void MerchantMenuHandler_SellItemAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.SellItemAccept(merchant);
        }

        private void MerchantMenuHandler_LearnSkillMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSkillMenu(merchant);
        }
        private void MerchantMenuHandler_LearnSkill(User user, Merchant merchant, ClientPacket packet)
        {
            var skillName = packet.ReadString8(); //skill name
            var skill = WorldData.GetByIndex<Castable>(skillName);
            user.ShowLearnSkill(merchant, skill);
        }
        private void MerchantMenuHandler_LearnSkillAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSkillAccept(merchant);
        }

        private void MerchantMenuHandler_LearnSkillAgree(User user, Merchant merchant, ClientPacket packet)
        {

            user.ShowLearnSkillAgree(merchant);
        }

        private void MerchantMenuHandler_LearnSkillDisagree(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSkillDisagree(merchant);
        }

        private void MerchantMenuHandler_LearnSpellMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSpellMenu(merchant);
        }
        private void MerchantMenuHandler_LearnSpell(User user, Merchant merchant, ClientPacket packet)
        {
            var spellName = packet.ReadString8();
            var spell = WorldData.GetByIndex<Castable>(spellName);
            user.ShowLearnSpell(merchant, spell);
        }
        private void MerchantMenuHandler_LearnSpellAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSpellAccept(merchant);
        }
        private void MerchantMenuHandler_LearnSpellAgree(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSpellAgree(merchant);
        }
        private void MerchantMenuHandler_LearnSpellDisagree(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSpellDisagree(merchant);
        }

        private void MerchantMenuHandler_ForgetSkillMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowForgetSkillMenu(merchant);
        }
        private void MerchantMenuHandler_ForgetSkill(User user, Merchant merchant, ClientPacket packet)
        {

        }
        private void MerchantMenuHandler_ForgetSkillAccept(User user, Merchant merchant, ClientPacket packet)
        {
            var slot = packet.ReadByte();

            user.ShowForgetSkillAccept(merchant, slot);
        }
        private void MerchantMenuHandler_ForgetSpellMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowForgetSpellMenu(merchant);
        }
        private void MerchantMenuHandler_ForgetSpell(User user, Merchant merchant, ClientPacket packet)
        {

        }
        private void MerchantMenuHandler_ForgetSpellAccept(User user, Merchant merchant, ClientPacket packet)
        {
            var slot = packet.ReadByte();

            user.ShowForgetSpellAccept(merchant, slot);
        }

        private void MerchantMenuHandler_SendParcelMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowMerchantSendParcel(merchant);
        }
        private void MerchantMenuHandler_SendParcelRecipient(User user, Merchant merchant, ClientPacket packet)
        {
            var item = packet.ReadByte();
            var itemObj = user.Inventory[item];
            user.ShowMerchantSendParcelRecipient(merchant, itemObj);
        }
        private void MerchantMenuHandler_SendParcel(User user, Merchant merchant, ClientPacket packet)
        {

        }

        private void MerchantMenuHandler_SendParcelFailure(User user, Merchant merchant, ClientPacket packet)
        {

        }
        private void MerchantMenuHandler_SendParcelAccept(User user, Merchant merchant, ClientPacket packet)
        {
            var recipient = packet.ReadString8();
            user.ShowMerchantSendParcelAccept(merchant, recipient);
        }
        #endregion Merchant Menu ItemObject Handlers
    }
}
