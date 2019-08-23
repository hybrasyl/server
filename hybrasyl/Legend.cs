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
 * (C) 2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *
 */
 
 using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Hybrasyl.Enums;
using Newtonsoft.Json;

namespace Hybrasyl
{

    [JsonObject(MemberSerialization.OptIn)]
    public class Legend : IEnumerable<LegendMark>
    {
        public const int MaximumLegendSize = 254;

        [JsonProperty]
        private SortedDictionary<DateTime, LegendMark> _legend =
            new SortedDictionary<DateTime, LegendMark>();

        private Dictionary<string, LegendMark> _legendIndex = new Dictionary<string, LegendMark>();

        public bool TryGetMark(string prefix, out LegendMark mark)
        {
            return _legendIndex.TryGetValue(prefix, out mark);
        }

        private bool _addLegendMark(LegendMark mark)
        {
            if (_legend.Keys.Count == MaximumLegendSize) return false;
            if (!string.IsNullOrEmpty(mark.Prefix) && _legendIndex.ContainsKey(mark.Prefix))
                throw new ArgumentException("A legend mark's prefix must be unique for a given character");
            _legend.Add(mark.Timestamp, mark);
            if (mark.Prefix != null)
                _legendIndex[mark.Prefix] = mark;
            return true;
        }

        public bool RemoveMark(string prefix)
        {
            LegendMark mark;
            if (!_legendIndex.TryGetValue(prefix, out mark)) return false;
            _legendIndex.Remove(prefix);
            _legend.Remove(mark.Timestamp);
            return true;
        }

        public bool AddMark(LegendIcon icon, LegendColor color, string text, DateTime timestamp,
            string prefix = default(string), bool isPublic = true, int quantity = 0, bool displaySeason = true, bool displayTimestamp = true)
        {
            var newMark = new LegendMark(icon, color, text, timestamp, prefix, isPublic, quantity, displaySeason, displayTimestamp);
            return _addLegendMark(newMark);
        }

        public bool AddMark(LegendIcon icon, LegendColor color, string text, string prefix = default(string),
            bool isPublic = true, int quantity = 0, bool displaySeason = true, bool displayTimestamp = true)
        {
            var datetime = DateTime.Now;
            var newMark = new LegendMark(icon, color, text, datetime, prefix, isPublic, quantity, displaySeason, displayTimestamp);
            return _addLegendMark(newMark);
        }

        [OnDeserialized]
        private void _CreateIndex(StreamingContext context)
        {
            RegenerateIndex();
        }

        public void RegenerateIndex()
        {
            if (_legendIndex.Count == _legend.Count) return;
            _legendIndex = new Dictionary<string, LegendMark>();
            foreach (var kvp in _legend)
            {
                _legendIndex[kvp.Value.Prefix] = kvp.Value;
            }
        }

        public void Clear()
        {
            _legendIndex = new Dictionary<string, LegendMark>();
            _legend = new SortedDictionary<DateTime, LegendMark>();
        }

        public int Count => _legend.Count;

        public IEnumerator<LegendMark> GetEnumerator()
        {
            return _legend.Values.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class LegendMark
    {
        [JsonProperty]
        public string Prefix { get; set; }
        [JsonProperty]
        public LegendColor Color { get; set; }
        [JsonProperty]
        public LegendIcon Icon { get; set; }
        [JsonProperty]
        public string Text { get; set; }
        [JsonProperty]
        public bool Public { get; set; }
        [JsonProperty]
        public bool DisplaySeason { get; set; }
        [JsonProperty]
        public bool DisplayTimestamp { get; set; }
        [JsonProperty]
        public DateTime Timestamp { get; set; }
        [JsonProperty]
        public DateTime Created { get; }
        [JsonProperty]
        public DateTime LastUpdated { get; set; }
        [JsonProperty]
        public int Quantity { get; set; }

        public HybrasylTime HybrasylDate => HybrasylTime.ConvertToHybrasyl(Timestamp);

        public LegendMark(LegendIcon icon, LegendColor color, string text, DateTime timestamp,
            string prefix = default(string), bool isPublic = true, int quantity = 0, bool displaySeason=true, bool displayTimestamp=true)
        {
            Icon = icon;
            Color = color;
            Text = text;
            Public = isPublic;
            Quantity = quantity;
            Prefix = prefix;
            Timestamp = timestamp;
            Created = DateTime.Now;
            LastUpdated = DateTime.Now;
            DisplaySeason = displaySeason;
            DisplayTimestamp = displayTimestamp;
        }

        public void AddQuantity(int quantity)
        {
            Quantity += quantity;
            LastUpdated = DateTime.Now;
        }

        public override string ToString()
        {
            var aislingDate = HybrasylTime.ConvertToHybrasyl(Timestamp != LastUpdated ? Timestamp : LastUpdated);
            var returnstring = Text;
            string markDate = "";
            if (DisplayTimestamp && DisplaySeason)
                markDate = $"{aislingDate.AgeName} {aislingDate.Year}, {aislingDate.Season}";
            else if (DisplayTimestamp)
                markDate = $"{aislingDate.AgeName} {aislingDate.Year}";

            var maxLength = 254 - 15 - markDate.Length;

            if (Text.Length > maxLength)
            {
                returnstring = Text.Substring(0, maxLength);
            }

            if (Quantity != 0)
                returnstring = $"{returnstring} ({Quantity})";
            if (!Public)
                returnstring = $" - {returnstring}";

            returnstring = $"{returnstring} - {markDate}";
            return returnstring;

        }
    }
}
