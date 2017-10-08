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
 */
 
using Hybrasyl.Config;
using Hybrasyl.Creatures;
using Hybrasyl.Items;
using Hybrasyl.Maps;
using Hybrasyl.Nations;
using System.Xml;
using System.Xml.Serialization;
using Castable = Hybrasyl.Castables.Castable;
using Map = Hybrasyl.Maps.Map;
using Npc = Hybrasyl.Creatures.Npc;

namespace Hybrasyl.XML
{
    public class Serializer : XMLBase
    {
        public static void Serialize(XmlWriter xWrite, Castable contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Actions");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, Creature contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Creature");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, Spawn contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Creature");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, SpawnGroup contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Creature");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, Npc contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Creature");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, Item contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Items");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, VariantGroup contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Items");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, Map contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Maps");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, WorldMap contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Maps");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, Nation contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Nations");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, Territory contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Nations");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, LootSet contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Loot");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, HybrasylConfig contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/Config");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static void Serialize(XmlWriter xWrite, Strings contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.hybrasyl.com/XML/HybrasylStrings");
            Writer.Serialize(xWrite, contents, ns);
        }

        public static string SerializeToString(object contents)
        {
            XmlSerializer Writer = new XmlSerializer(contents.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            Utf8StringWriter stringWriter = new Utf8StringWriter();
            Writer.Serialize(stringWriter, contents, ns);
            return stringWriter.ToString();
        }

        public static Castable Deserialize(XmlReader reader, Castable contents = null)
        {
            if (contents == null) contents = new Castable();
            //reader.Settings.IgnoreWhitespace = false;
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Castable)xContents;
            }
            return contents;
        }

        public static Creature Deserialize(XmlReader reader, Creature contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new Creature();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Creature)xContents;
            }
            return contents;
        }

        public static Spawn Deserialize(XmlReader reader, Spawn contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new Spawn();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Spawn)xContents;
            }
            return contents;
        }

        public static SpawnGroup Deserialize(XmlReader reader, SpawnGroup contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new SpawnGroup();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (SpawnGroup)xContents;
            }
            return contents;
        }

        public static Npc Deserialize(XmlReader reader, Npc contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new Npc();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Npc)xContents;
            }
            return contents;
        }

        public static Item Deserialize(XmlReader reader, Item contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new Item();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Item)xContents;
            }
            return contents;
        }

        public static VariantGroup Deserialize(XmlReader reader, VariantGroup contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new VariantGroup();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (VariantGroup)xContents;
            }
            return contents;
        }

        public static Map Deserialize(XmlReader reader, Map contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new Map();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Map)xContents;
            }
            return contents;
        }

        public static WorldMap Deserialize(XmlReader reader, WorldMap contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new WorldMap();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (WorldMap)xContents;
            }
            return contents;
        }

        public static Nation Deserialize(XmlReader reader, Nation contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new Nation();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Nation)xContents;
            }
            return contents;
        }

        public static Territory Deserialize(XmlReader reader, Territory contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new Territory();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Territory)xContents;
            }
            return contents;
        }

        public static LootSet Deserialize(XmlReader reader, LootSet contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new LootSet();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (LootSet)xContents;
            }
            return contents;
        }

        public static HybrasylConfig Deserialize(XmlReader reader, HybrasylConfig contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new HybrasylConfig();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (HybrasylConfig)xContents;
            }
            return contents;
        }

        public static Strings Deserialize(XmlReader reader, Strings contents = null)
        {
            //reader.Settings.IgnoreWhitespace = false;
            if (contents == null) contents = new Strings();
            XmlSerializer XmlSerial = new XmlSerializer(contents.GetType());
            if (XmlSerial.CanDeserialize(reader))
            {
                var xContents = XmlSerial.Deserialize(reader);
                contents = (Strings)xContents;
            }
            return contents;
        }
    }
}