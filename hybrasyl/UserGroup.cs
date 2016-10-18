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
 * Authors:   Luke Segars   <luke@lukesegars.com>
 */

using Hybrasyl.Objects;
using Hybrasyl.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using log4net;

namespace Hybrasyl
{
    /**
     * This class defines a group of users. Grouped users can whisper to the full group
     * and will also split experience with nearby group members.
     */

    public class UserGroup
    {
        public static readonly ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Group-related info
        public List<User> Members { get; private set; }
        public DateTime CreatedOn { get; private set; }
        public Dictionary<Class, uint> ClassCount { get; private set; }
        public uint MaxMembers = 0;

        private delegate Dictionary<uint, int> DistributionFunc(User source, int full);

        private DistributionFunc ExperienceDistributionFunc;

        public UserGroup(User founder)
        {
            Members = new List<User>();
            ClassCount = new Dictionary<Class, uint>();
            foreach (var cl in Enum.GetValues(typeof(Class)).Cast<Class>())
            {
                ClassCount[cl] = 0;
            }

            Logger.InfoFormat("Creating new group with {0} as founder.", founder.Name);
            // Distribute full experience to everyone with a bonus if a member of each
            // class is present.
            ExperienceDistributionFunc = Distribution_AllClassBonus;

            Add(founder);
            CreatedOn = DateTime.Now;
        }

        // Group-related functions and accessors
        public bool Add(User user)
        {
            // You can only join a group if you're not already a member in another group.
            // Check this condition and notify the invitee and existing group members if
            // there's an issue.
            if (user.Grouped)
            {
                user.SendMessage("You're already in a group.", MessageTypes.SYSTEM);

                foreach (var member in Members)
                {
                    member.SendMessage(String.Format("{0} is in another group.", user.Name), MessageTypes.SYSTEM);
                }

                // If this fails when the group only contains one other person, the group should be abandoned.
                if (Count == 1)
                {
                    Remove(Members[0]);
                }

                return false;
            }

            // Send a message to notify everyone else that someone's joined.
            foreach (var member in Members)
            {
                member.SendMessage(user.Name + " has joined your group.", MessageTypes.SYSTEM);
            }

            Members.Add(user);
            user.Group = this;
            ClassCount[user.Class]++;
            MaxMembers = (uint) Math.Max(MaxMembers, Members.Count);

            // Send a distinct message to the new user.
            user.SendMessage("You've joined a group.", MessageTypes.SYSTEM);
            return true;
        }

        public void Remove(User user)
        {
            Members.Remove(user);
            user.Group = null;
            ClassCount[user.Class]--;

            // If this has ever been a true group from a user's perspective, talk about it. Otherwise
            // don't send user-facing messages.
            if (MaxMembers > 1)
            {
                // User has already been removed from Members so no need to exclude.
                foreach (var member in Members)
                {
                    member.SendMessage(user.Name + " has left your group.", MessageTypes.SYSTEM);
                }
                user.SendMessage("You've left a group.", MessageTypes.SYSTEM);
            }

            // If there's only one person left, disband the group.
            if (Count == 1)
            {
                Remove(Members[0]);
            }
        }

        public int Count
        {
            get { return Members.Count; }
        }

        public bool ContainsAllClasses()
        {
            return (ClassCount[Enums.Class.Monk] > 0 &&
                    ClassCount[Enums.Class.Priest] > 0 &&
                    ClassCount[Enums.Class.Rogue] > 0 &&
                    ClassCount[Enums.Class.Warrior] > 0 &&
                    ClassCount[Enums.Class.Wizard] > 0);
        }

        /**
         * Find out whether a given user is close enough to the action in order
         * to receive a share. It's not OK to be (a) on a different map or
         * (b) really far away on the same map.
         */

        private bool WithinRange(User user, User target)
        {
            if (user.Map.Id == target.Map.Id)
            {
                int xDelta = Math.Abs(user.Map.X - target.Map.X);
                int yDelta = Math.Abs(user.Map.Y - target.Map.Y);

                return (xDelta + yDelta < Constants.GROUP_SHARING_DISTANCE);
            }

            return false;
        }

        /**
         * Distribute a pool of experience across members of the group.
         */

        public void ShareExperience(User source, int experience)
        {
            Dictionary<uint, int> share = ExperienceDistributionFunc(source, experience);

            for (int i = 0; i < Members.Count; i++)
            {
                // Note: this will only work for positive numbers at this point.
                Members[i].GiveExperience((uint) share[Members[i].Id]);
            }
        }

        /**
         * Distribution functions. Should be assigned to ExperienceDistributionFunc or a similar
         * function to be put to use. See the constructor of UserGroup for an example. Note that
         * these are intended to be generic and can apply to any numeric quantity (experience,
         * gold, even #'s of items).
         */

        /**
         * This distribution function gives the full quantity of a resource to each of the
         * group members.
         */

        private Dictionary<uint, int> Distribution_Full(User source, int full)
        {
            Dictionary<uint, int> share = new Dictionary<uint, int>();

            foreach (var member in Members)
            {
                share[member.Id] = WithinRange(member, source) ? full : 0;
            }

            return share;
        }

        /**
         * Give the full quantity of a resource to each of the group members, +10% bonus
         * if there's at least one representative from each class.
         */

        private Dictionary<uint, int> Distribution_AllClassBonus(User source, int full)
        {
            // Check to see if at least one representative from each class is in the group.
            if (ContainsAllClasses())
            {
                full = (int) (full*1.10);
            }

            return Distribution_Full(source, full);
        }

        /// <summary>
        /// Send a system message as a group message, that the entire group can see.
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(string message)
        {
            foreach (var member in Members)
                member.SendMessage($"[Notice] {message}", MessageTypes.GROUP);
        }
    }
}
