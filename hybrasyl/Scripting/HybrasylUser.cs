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
using System.Linq;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Items;
using Hybrasyl.Objects;
using log4net;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class HybrasylUser
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        internal User User { get; set; }
        internal HybrasylWorld World { get; set; }
        internal HybrasylMap Map { get; set; }
        public string Name => User.Name;
        public byte X => User.X;
        public byte Y => User.Y;

        public Sex Sex => User.Sex;

        public uint Hp
        {
            get { return User.Stats.Hp; }
            set {
                User.Stats.Hp = value;
                User.UpdateAttributes(StatUpdateFlags.Current);
            }
        }

        public uint Mp
        {
            get { return User.Stats.Mp; }
            set
            {
                User.Stats.Mp = value;
                User.UpdateAttributes(StatUpdateFlags.Current);
            }
        }

        public HybrasylUser(User user)
        {
            User = user;
            World = new HybrasylWorld(user.World);
            Map = new HybrasylMap(user.Map);
        }

        public List<HybrasylWorldObject> GetViewportObjects()
        {
            return new List<HybrasylWorldObject>();
        }

        public List<HybrasylUser> GetViewportPlayers()
        {
            return new List<HybrasylUser>();
        }

        public void Resurrect()
        {
            User.Resurrect();
        }

        public HybrasylUser GetFacingUser()
        {
            var facing = User.GetFacingUser();
            return facing != null ? new HybrasylUser(facing) : null;
        }

        public List<HybrasylWorldObject> GetFacingObjects()
        {
            return User.GetFacingObjects().Select(item => new HybrasylWorldObject(item)).ToList();
        }

        public void EndComa()
        {
            User.EndComa();
        }

        public dynamic GetLegendMark(string prefix)
        {
            LegendMark mark;
            return User.Legend.TryGetMark(prefix, out mark) ? mark : (object)null;
        }

        public Legend GetLegend()
        {
            return User.Legend;
        }

        public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, string prefix=default(string), bool isPublic = true, int quantity = 0)
        {
            return AddLegendMark(icon, color, text, DateTime.Now, prefix, isPublic, quantity);
        }

        public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, DateTime created, string prefix = default(string), bool isPublic = true, int quantity = 0)
        {
            try
            {
                return User.Legend.AddMark(icon, color, text, created, prefix, isPublic, quantity);
            }
            catch (ArgumentException)
            {
                Logger.ErrorFormat("Legend mark: {0}: duplicate prefix {1}", User.Name, prefix);
            }
            return false;
        }

        public bool RemoveLegendMark(string prefix)
        {
            return User.Legend.RemoveMark(prefix);
        }

        public bool ModifyLegendMark(string prefix, int quantity, bool isPublic)
        {
            LegendMark mark;
            if (!User.Legend.TryGetMark(prefix, out mark)) return false;
            mark.Quantity = quantity;
            mark.Public = isPublic;
            return true;
        }

        public void SetSessionCookie(string cookieName, dynamic value)
        {
            try
            {
                if (value.GetType() == typeof(string))
                    User.SetSessionCookie(cookieName, value);
                else
                    User.SetSessionCookie(cookieName, value.ToString());
                Logger.DebugFormat("{0} - set session cookie {1} to {2}", User.Name, cookieName, value);
            }
            catch (Exception e)
            {
                Logger.WarnFormat("{0}: value could not be converted to string? {1}", User.Name, e.ToString());
            }
        }

        public void SetCookie(string cookieName, dynamic value)
        { 
            try
            {
                if (value.GetType() == typeof(string))
                    User.SetSessionCookie(cookieName, value);
                else
                    User.SetCookie(cookieName, value.ToString());
                Logger.DebugFormat("{0} - set cookie {1} to {2}", User.Name, cookieName, value);
            }
            catch (Exception e)
            {
                Logger.WarnFormat("{0}: value could not be converted to string? {1}", User.Name, e.ToString());
            }

        }

        public string GetSessionCookie(string cookieName) => User.GetSessionCookie(cookieName);

        public string GetCookie(string cookieName) => User.GetCookie(cookieName);

        public bool HasCookie(string cookieName) => User.HasCookie(cookieName);
        public bool HasSessionCookie(string cookieName) => User.HasSessionCookie(cookieName);
        public bool DeleteCookie(string cookieName) => User.DeleteCookie(cookieName);
        public bool DeleteSessionCookie(string cookieName) => User.DeleteSessionCookie(cookieName);

        public void DisplayEffect(ushort effect, short speed = 100, bool global = true)
        {
            if (!global)
                User.SendEffect(User.Id, effect, speed);
            else
                User.Effect(effect, speed);
        }

        public void DisplayEffectAtCoords(short x, short y, ushort effect, short speed = 100, bool global = true)
        {
            if (!global)
                User.SendEffect(x, y, effect, speed);
            else
                User.Effect(x, y, effect, speed);
        }

        public void Teleport(string location, int x, int y)
        {
            User.Teleport(location, (byte)x, (byte)y);
        }

        public void SoundEffect(byte sound)
        {
            User.SendSound(sound);
        }

        public void HealToFull()
        {
            User.Heal(User.Stats.MaximumHp);
        }

        public void Heal(int heal)
        {
            User.Heal((double)heal);
        }

        public void Damage(int damage, Enums.Element element = Enums.Element.None,
           Enums.DamageType damageType = Enums.DamageType.Direct)
        {
            User.Damage((double)damage, element, damageType);
        }

        public bool GiveItem(HybrasylWorldObject obj)
        {
            if (obj.Obj is ItemObject)
                return User.AddItem(obj.Obj as ItemObject);
            return false;
        }

        public bool GiveItem(string name, int count = 1)
        {
            // Does the item exist?
            if (Game.World.WorldData.TryGetValueByIndex(name, out Item template))
            {
                var item = Game.World.CreateItem(template.Id);
                if (count > 1)
                    item.Count = count;
                else
                    item.Count = item.MaximumStack;
                Game.World.Insert(item);
                User.AddItem(item);
                return true;
            }
            return false;
        }    

        public bool TakeItem(string name)
        {
            return false;
        }

        public bool GiveExperience(int exp)
        {
            SystemMessage($"{exp} experience!");
            User.GiveExperience((uint)exp);
            return true;
        }

        public bool TakeExperience(int exp)
        {
            User.Stats.Experience -= (uint)exp;
            SystemMessage($"Your world spins as your insight leaves you ((-{exp} experience!))");
            User.UpdateAttributes(StatUpdateFlags.Experience);
            return true;
        }

        public void SystemMessage(string message)
        {
            // This is a typical client "orange message"
            User.SendMessage(message, Hybrasyl.MessageTypes.SYSTEM_WITH_OVERHEAD);
        }

        public void Whisper(string name, string message)
        {
            User.SendWhisper(name, message);
        }

        public void Mail(string name, string message)
        {
        }

        public void StartDialogSequence(string sequenceName, HybrasylWorldObject associate)
        {
            DialogSequence newSequence;
            if (User.World.GlobalSequencesCatalog.TryGetValue(sequenceName, out newSequence))
            {
                newSequence.ShowTo(User, (VisibleObject)associate.Obj);
                // End previous sequence
                User.DialogState.EndDialog();
                User.DialogState.StartDialog(associate.Obj as VisibleObject, newSequence);
            }

        }

        public void EndDialog() => User.DialogState.EndDialog();

        public void StartSequence(string sequenceName, HybrasylWorldObject associateOverride = null)
        {
            DialogSequence sequence;
            VisibleObject associate;
            Logger.DebugFormat("{0} starting sequence {1}", User.Name, sequenceName);

            // If we're using a new associate, we will consult that to find our sequence
            associate = associateOverride == null ? User.DialogState.Associate as VisibleObject : associateOverride.Obj as VisibleObject;

            // Use the local catalog for sequences first, then consult the global catalog

            if (!associate.SequenceCatalog.TryGetValue(sequenceName, out sequence) && !Game.World.GlobalSequencesCatalog.TryGetValue(sequenceName, out sequence))
            {
                Logger.ErrorFormat("called from {0}: sequence name {1} cannot be found!",
                    associate.Name, sequenceName);
                // To be safe, terminate all dialog state
                User.DialogState.EndDialog();
                if (associate is Merchant)
                    // If the user was previously talking to a merchant, and we can't find a sequence,
                    // simply display the main menu again. If it's a reactor....oh well.
                    associate.DisplayPursuits(User);
                return;
            }
            
            // sequence should now be our target sequence, let's end the current state and start a new one

            User.DialogState.EndDialog();
            User.DialogState.StartDialog(associate, sequence);
            User.DialogState.ActiveDialog.ShowTo(User, associate);
        }

        /// <summary>
        /// Calculate the Manhattan distance between ourselves and a target object.
        /// </summary>
        /// <param name="target">The target object</param>
        /// <returns></returns>
        public int Distance(HybrasylWorldObject target) => User.Distance(target.Obj);
    }
}
