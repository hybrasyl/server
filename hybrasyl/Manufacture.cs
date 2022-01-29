using Hybrasyl.Objects;
using System;
using System.Collections.Generic;

namespace Hybrasyl
{
    public class ManufactureState
    {
        public ManufactureState(User user)
            : this(user, Array.Empty<ManufactureRecipe>())
        {
        }

        public ManufactureState(User user, IEnumerable<ManufactureRecipe> recipes)
        {
            User = user;
            Recipes = new List<ManufactureRecipe>(recipes);
        }

        public User User { get; }

        public List<ManufactureRecipe> Recipes { get; }

        public int SelectedIndex { get; private set; }

        public ManufactureRecipe SelectedRecipe => Recipes[SelectedIndex];

        public void ProcessManufacturePacket(ClientPacket packet)
        {
            var manualType = packet.ReadByte();
            var slotIndex = packet.ReadByte();
            var manualAction = (ManufactureClientPacketType)packet.ReadByte();

            if (manualAction == ManufactureClientPacketType.RequestPage)
            {
                var pageIndex = packet.ReadByte();

                if (Math.Abs(SelectedIndex - pageIndex) != 1 || pageIndex >= Recipes.Count)
                {
                    return;
                }

                ShowPage(pageIndex);
            }
            else if (manualAction == ManufactureClientPacketType.Make)
            {
                var recipeName = packet.ReadString8();
                var addSlotIndex = packet.ReadByte();
                MakeRecipe(recipeName, addSlotIndex);
            }
        }

        public void ShowWindow()
        {
            var manufacturePacket = new ServerPacket(0x50);
            manufacturePacket.WriteByte(1);
            manufacturePacket.WriteByte(60);
            manufacturePacket.WriteByte((byte)ManufactureServerPacketType.Open);
            User.Enqueue(manufacturePacket);
        }

        public void ShowPage(int pageIndex)
        {
            SelectedIndex = pageIndex;

            var manufacturePacket = new ServerPacket(0x50);
            manufacturePacket.WriteByte(1);
            manufacturePacket.WriteByte(60);
            manufacturePacket.WriteByte((byte)ManufactureServerPacketType.Page);
            manufacturePacket.WriteByte((byte)pageIndex);
            manufacturePacket.WriteUInt16((ushort)(SelectedRecipe.Tile + 0x8000));
            manufacturePacket.WriteString8(SelectedRecipe.Name);
            manufacturePacket.WriteString16(SelectedRecipe.Description);
            manufacturePacket.WriteString16(SelectedRecipe.IngredientsText);
            manufacturePacket.WriteBoolean(SelectedRecipe.HasAddItem);
            User.Enqueue(manufacturePacket);
        }

        public bool MakeRecipe(string recipeName, int addSlotIndex)
        {
            if (recipeName != SelectedRecipe.Name)
            {
                return false;
            }

            if (!ConfirmIngredients())
            {
                User.SendSystemMessage("You do not have all the ingredients for that recipe.");
                return false;
            }

            TakeIngredients();
            GiveManufacturedItem();

            return true;
        }

        public bool ConfirmIngredients()
        {
            return SelectedRecipe.ConfirmIngredientsFor(User);
        }

        public void TakeIngredients()
        {
            SelectedRecipe.TakeIngredientsFrom(User);
        }

        public void GiveManufacturedItem()
        {
            SelectedRecipe.GiveManufacturedItemTo(User);
        }
    }

    public class ManufactureRecipe
    {
        public string Name { get; set; }

        public ushort Tile { get; set; }

        public string Description { get; set; }

        public List<ManufactureIngredient> Ingredients { get; set; }

        public bool HasAddItem { get; set; }

        public string IngredientsText
        {
            get
            {
                List<string> ingredientLines = new();
                foreach (var ingredient in Ingredients)
                {
                    ingredientLines.Add($"{ingredient.Name} ({ingredient.Quantity}");
                }
                return string.Join("\n", ingredientLines);
            }
        }

        public bool ConfirmIngredientsFor(User user)
        {
            foreach (var ingredient in Ingredients)
            {
                if (!ingredient.ConfirmFor(user))
                {
                    return false;
                }
            }
            return true;
        }

        public void TakeIngredientsFrom(User user)
        {
            foreach (var ingredient in Ingredients)
            {
                ingredient.TakeFrom(user);
            }
        }

        public void GiveManufacturedItemTo(User user)
        {
            user.AddItem(Name);
        }
    }

    public class ManufactureIngredient
    {
        public string Name { get; set; }

        public int Quantity { get; set; }

        public bool ConfirmFor(User user)
        {
            int count = 0;
            foreach (var item in user.Inventory)
            {
                if (item.Name == Name)
                {
                    count += item.Count;
                }
            }
            return count >= Quantity;
        }

        public void TakeFrom(User user)
        {
            user.RemoveItem(Name, (ushort)Quantity);
        }
    }

    public enum ManufactureClientPacketType
    {
        RequestPage = 0,
        Make = 1,
    }

    public enum ManufactureServerPacketType
    {
        Open = 0,
        Page = 1,
    }
}
