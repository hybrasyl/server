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
using C3;
using Hybrasyl.Dialogs;
using log4net;
using Newtonsoft.Json;

namespace Hybrasyl.Objects
{
    [JsonObject(MemberSerialization.OptIn)]
    public class WorldObject : IQuadStorable
    {
        public static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The rectangle that defines the object's boundaries.
        /// </summary>
        public Rectangle Rect => new Rectangle(X, Y, 1, 1);

        public bool HasMoved { get; set; }

        public byte X { get; set; }
        public byte Y { get; set; }
        public uint Id { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        public Script Script { get; set; }
        public World World { get; set; }

        public WorldObject()
        {
            Name = string.Empty;
            ResetPursuits();
        }

        public virtual void SendId()
        {
        }

        public void ResetPursuits()
        {
            Pursuits = new List<DialogSequence>();
            DialogSequences = new List<DialogSequence>();
            SequenceCatalog = new Dictionary<string, DialogSequence>();
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

            if (SequenceCatalog.ContainsKey(pursuit.Name))
            {
                Logger.WarnFormat("Pursuit {0} is being overwritten", pursuit.Name);
                SequenceCatalog.Remove(pursuit.Name);

            }

            SequenceCatalog.Add(pursuit.Name, pursuit);

            if (pursuit.Id > Constants.DIALOG_SEQUENCE_SHARED)
            {
                pursuit.AssociateSequence(this);
            }
        }

        public virtual void RegisterDialogSequence(DialogSequence sequence)
        {
            sequence.Id = (uint)(Constants.DIALOG_SEQUENCE_PURSUITS + DialogSequences.Count);
            DialogSequences.Add(sequence);
            if (SequenceCatalog.ContainsKey(sequence.Name))
            {
                Logger.WarnFormat("Dialog sequence {0} is being overwritten", sequence.Name);
                SequenceCatalog.Remove(sequence.Name);

            }
            SequenceCatalog.Add(sequence.Name, sequence);
        }

        public List<DialogSequence> Pursuits;
        public List<DialogSequence> DialogSequences;
        public Dictionary<String, DialogSequence> SequenceCatalog;
    }


}
