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

namespace Hybrasyl.Objects;

public class Gold : VisibleObject
{
    public uint Amount { get; set; }

    public override string Name
    {
        get
        {
            if (Amount == 1) return "Copper Coin";
            if (Amount < 10) return "Copper Pile";
            if (Amount < 100) return "Silver Coin";
            if (Amount < 1000) return "Silver Pile";
            if (Amount < 10000) return "Gold Coin";
            return "Gold Pile";
        }
    }

    public new ushort Sprite
    {
        get
        {
            if (Amount == 1) return 139;
            if (Amount < 10) return 142;
            if (Amount < 100) return 138;
            if (Amount < 1000) return 141;
            if (Amount < 10000) return 137;
            return 140;
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