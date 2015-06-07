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
 *            
 */

namespace Hybrasyl.Objects
{
    public class Gold : VisibleObject
    {
        public uint Amount { get; set; }

        public new string Name
        {
            get
            {
                if (Amount == 1)
                {
                    return "Silver Coin";
                }
                else
                {
                    if (Amount < 100)
                    {
                        return "Gold Coin";
                    }
                    else
                    {
                        if (Amount < 1000)
                        {
                            return "Silver Pile";
                        }
                        else
                        {
                            return "Gold Pile";
                        }
                    }
                }
            }
        }
        public new ushort Sprite
        {
            get
            {
                if (Amount == 1)
                {
                    return 138;
                }
                else
                {
                    if (Amount < 100)
                    {
                        return 137;
                    }
                    else
                    {
                        if (Amount < 1000)
                        {
                            return 141;
                        }
                        else
                        {
                            return 140;
                        }
                    }
                }
            }
        }

        public Gold(uint amount)
        {
            Amount = amount;
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                user.SendVisibleGold(this);
            }
        }
    }
}
