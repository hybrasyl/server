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
using Hybrasyl.Enums;
using Newtonsoft.Json;

namespace Hybrasyl
{

    [JsonObject(MemberSerialization.OptIn)]
    public class Legend : IEnumerable<LegendMark>
    {
        public const int MaximumLegendSize = 254;

        [JsonProperty] private SortedDictionary<DateTime, LegendMark> _legend =
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
            _legend.Add(mark.Created, mark);
            _legendIndex[mark.Prefix] = mark;
            return true;
        }

        public bool RemoveMark(string prefix)
        {
            LegendMark mark;
            if (!_legendIndex.TryGetValue(prefix, out mark)) return false;
            _legendIndex.Remove(prefix);
            _legend.Remove(mark.Created);
            return true;
        }

        public bool AddMark(LegendIcon icon, LegendColor color, string text, DateTime created,
            string prefix = default(string), bool isPublic = true, int quantity = 0)
        {
            var newMark = new LegendMark(icon, color, text, created, prefix, isPublic, quantity);
            return _addLegendMark(newMark);
        }

        public bool AddMark(LegendIcon icon, LegendColor color, string text, string prefix = default(string),
            bool isPublic = true, int quantity = 0)
        {
            var datetime = DateTime.Now;
            var newMark = new LegendMark(icon, color, text, datetime, prefix, isPublic, quantity);
            return _addLegendMark(newMark);
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

    [JsonObject]
    public class LegendMark
    {
        public string Prefix { get; set; }
        public LegendColor Color { get; set; }
        public LegendIcon Icon { get; set; }
        public string Text { get; set; }
        public bool Public { get; set; }
        public DateTime Created { get; }
        public DateTime LastUpdated { get; set; }
        public int Quantity { get; set; }

        public LegendMark(LegendIcon icon, LegendColor color, string text, DateTime created,
            string prefix = default(string), bool isPublic = true, int quantity = 0)
        {
            Icon = icon;
            Color = color;
            Text = text;
            Public = isPublic;
            Quantity = quantity;
            Prefix = prefix;
            Created = created;
            LastUpdated = created;
        }

        public void AddQuantity(int quantity)
        {
            Quantity += quantity;
            LastUpdated = DateTime.Now;
        }

        public override string ToString()
        {
            var aislingDate = HybrasylTime.ConvertToHybrasyl(Created != LastUpdated ? Created : LastUpdated);
            var returnString = Text;
            var markDate = $"{aislingDate.Age} {aislingDate.Year}, {aislingDate.Season}";

            var maxLength = 254 - 15 - markDate.Length;

            if (Text.Length > maxLength)
            {
                returnString = Text.Substring(0, maxLength);
            }

            if (Quantity != 0)
                returnString = $"{returnString} ({Quantity})";
            if (!Public)
                returnString = $" - {returnString}";

            returnString = $"{returnString} - {markDate}";
            return returnString;

        }
    }
}
