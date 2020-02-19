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
using Hybrasyl.Castables;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Items;
using Hybrasyl.Objects;
using Serilog;
using MoonSharp.Interpreter;
using System.Reflection;

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class HybrasylUser
    {
        internal User User { get; set; }
        internal HybrasylWorld World { get; set; }
        internal HybrasylMap Map { get; set; }
        public string Name => User.Name;
        public byte X => User.X;
        public byte Y => User.Y;
        public Enums.Class Class => User.Class;

        // TODO: determine a better way to do this in lua via moonsharp
        public string Type => "player";


        public Sex Sex => User.Sex;

        public uint Hp
        {
            get { return User.Stats.Hp; }
            set {
                User.Stats.Hp = value;
                User.UpdateAttributes(StatUpdateFlags.Current);
            }
        }

        public int Level { get => User.Stats.Level; }

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

        public void ChangeClass(Enums.Class newClass, string oathGiver)
        {
            User.Class = newClass;
            User.UpdateAttributes(StatUpdateFlags.Full);
            LegendIcon icon;
            string legendtext;
            // this is annoying af
            switch (newClass)
            {
                case Enums.Class.Monk:
                    icon = LegendIcon.Monk;
                    legendtext = $"Monk by oath of {oathGiver}";
                    break;
                case Enums.Class.Priest:
                    icon = LegendIcon.Priest;
                    legendtext = $"Priest by oath of {oathGiver}";
                    break;
                case Enums.Class.Rogue:
                    icon = LegendIcon.Rogue;
                    legendtext = $"Rogue by oath of {oathGiver}";
                    break;
                case Enums.Class.Warrior:
                    icon = LegendIcon.Warrior;
                    legendtext = $"Warrior by oath of {oathGiver}";
                    break;
                case Enums.Class.Wizard:
                    icon = LegendIcon.Wizard;
                    legendtext = $"Wizard by oath of {oathGiver}";
                    break;
                default:
                    throw new ArgumentException("Invalid class");
            }
            User.Legend.AddMark(icon, LegendColor.White, legendtext, "CLS");
        }
        public Legend GetLegend()
        {
            return User.Legend;
        }

        public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, string prefix=default(string), bool isPublic = true, 
            int quantity = 0, bool displaySeason=true, bool displayTimestamp=true)
        {
            return AddLegendMark(icon, color, text, DateTime.Now, prefix, isPublic, quantity, displaySeason, displayTimestamp);
        }

        public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, HybrasylTime timestamp, string prefix) => User.Legend.AddMark(icon, color, text, timestamp.TerranDateTime, prefix);

        public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, DateTime timestamp, string prefix = default(string), 
            bool isPublic = true, int quantity = 0, bool displaySeason=true, bool displayTimestamp=true)
        {
            try
            {
                return User.Legend.AddMark(icon, color, text, timestamp, prefix, isPublic, quantity, displaySeason, displayTimestamp);
            }
            catch (ArgumentException)
            {
                GameLog.ErrorFormat("Legend mark: {0}: duplicate prefix {1}", User.Name, prefix);
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence">The sequence name to start</param>
        /// <param name="target">The </param>
        /// <returns></returns>
        public bool RequestDialog(string sequence, string invoker="")
        {
            DialogSequence sequenceObj = null;
            VisibleObject invokerObj = null;

            if (Game.World.TryGetActiveUser(invoker, out User user))
                invokerObj = user as VisibleObject;
            else if (Game.World.WorldData.TryGetValue<Merchant>(invoker, out Merchant merchant))
                invokerObj = merchant as VisibleObject;

            if (invokerObj != null)
                invokerObj.SequenceCatalog.TryGetValue(sequence, out sequenceObj);

            if (sequenceObj == null)
                // Try global catalog
                Game.World.GlobalSequences.TryGetValue(sequence, out sequenceObj);

            if (invokerObj != null && sequenceObj != null)
                return Game.World.TryAsyncDialog(invokerObj, User, sequenceObj);

            GameLog.Warning($"invoker {invoker} or sequence {sequence} not found");
            return false;
        }
           
        public void SetSessionCookie(string cookieName, dynamic value)
        {
            try
            {
                if (value.GetType() == typeof(string))
                    User.SetSessionCookie(cookieName, value);
                else
                    User.SetSessionCookie(cookieName, value.ToString());
                GameLog.DebugFormat("{0} - set session cookie {1} to {2}", User.Name, cookieName, value);
            }
            catch (Exception e)
            {
                GameLog.WarningFormat("{0}: value could not be converted to string? {1}", User.Name, e.ToString());
            }
        }

        public void SetCookie(string cookieName, dynamic value)
        { 
            try
            {
                if (value.GetType() == typeof(string))
                    User.SetCookie(cookieName, value);
                else
                    User.SetCookie(cookieName, value.ToString());
                GameLog.DebugFormat("{0} - set cookie {1} to {2}", User.Name, cookieName, value);
            }
            catch (Exception e)
            {
                GameLog.WarningFormat("{0}: value could not be converted to string? {1}", User.Name, e.ToString());
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

        public void Damage(int damage, bool fatal=true)
        {
            if (fatal)
                User.Damage((double)damage, Enums.Element.None, Enums.DamageType.Direct, Castables.DamageFlags.Nonlethal);
            else
                User.Damage((double)damage, Enums.Element.None, Enums.DamageType.Direct);

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
            if (User.Inventory.ContainsName(name))
            {
                // Find the first instance of the specified item and remove it
                var slots = User.Inventory.SlotByName(name);
                if (slots.Length == 0)
                {
                    GameLog.ScriptingWarning("{Function}: User had {item} a moment ago but now it's gone...?",
                        MethodInfo.GetCurrentMethod().Name, User.Name, name);
                    return false;
                }
                GameLog.ScriptingDebug("{Function}: A script removed {item} from {User}'s inventory ",
                    MethodInfo.GetCurrentMethod().Name, User.Name, name); return User.Inventory.Remove(slots.First());
            }
            GameLog.ScriptingDebug("{Function}: User {User} doesn't have {item}",
                MethodInfo.GetCurrentMethod().Name, User.Name, name);
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
            if ((uint)exp > User.Stats.Experience)
                return false;
            User.Stats.Experience -= (uint)exp;
            SystemMessage($"Your world spins as your insight leaves you ((-{exp} experience!))");
            User.UpdateAttributes(StatUpdateFlags.Experience);
            return true;
        }

        public bool AddSkill(string skillname)
        {
            if (Game.World.WorldData.TryGetValue(skillname, out Castable result))
            {
                User.AddSkill(result);
                return true;
            }
            return false;
        }

        public void SystemMessage(string message)
        {
            // This is a typical client "orange message"
            User.SendMessage(message, Hybrasyl.MessageTypes.SYSTEM_WITH_OVERHEAD);
        }

        public bool IsPeasant() => User.Class == Enums.Class.Peasant;

        public void Whisper(string name, string message)
        {
            User.SendWhisper(name, message);
        }

        public void Mail(string name, string message)
        {
        }

        public void EndDialog()
        {
            User.DialogState.EndDialog();
            User.SendCloseDialog();
        }

        public void StartSequence(string sequenceName, HybrasylWorldObject associateOverride = null)
        {
            DialogSequence sequence = null;
            VisibleObject associate = null;
            GameLog.DebugFormat("{0} starting sequence {1}", User.Name, sequenceName);

            // First: is this a global sequence?
            Game.World.GlobalSequences.TryGetValue(sequenceName, out sequence);

            // Next: what object are we associated with?
            if (associateOverride == null)
            {
                if (User.DialogState.Associate != null)
                    associate = User.DialogState.Associate as VisibleObject;
                else if (User.LastAssociate != null)
                    associate = User.LastAssociate;
            }
            else
                associate = associateOverride.Obj as VisibleObject;

            // If we didn't get a sequence before, try with our associate
            if (sequence == null && associate != null)
                associate.SequenceCatalog.TryGetValue(sequenceName, out sequence);

            // We should hopefully have a sequence now...
            if (sequence == null)
            {
                GameLog.ErrorFormat("called from {0}: sequence name {1} cannot be found!",
                    associate?.Name ?? "globalsequence", sequenceName);
                // To be safe, terminate all dialog state
                User.DialogState.EndDialog();
                // If the user was previously talking to a merchant, and we can't find a sequence,
                // simply display the main menu again. If it's a reactor....oh well.
                if (associate is Merchant)
                    associate.DisplayPursuits(User);
                return;
            }

            // If we're here, sequence should now be our target sequence, 
            // let's end the current state and start a new one

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
