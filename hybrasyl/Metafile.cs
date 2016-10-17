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
 * Authors:   Kyle Speck    <kojasou@hybrasyl.com>
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hybrasyl
{
    public class Metafile
    {
        public string Name { get; set; }
        public List<MetafileNode> Nodes { get; private set; }
        public Metafile(string name)
        {
            Name = name;
            Nodes = new List<MetafileNode>();
        }
        public Metafile(string name, params MetafileNode[] elements)
        {
            Name = name;
            Nodes = new List<MetafileNode>(elements);
        }
        public CompiledMetafile Compile()
        {
            return new CompiledMetafile(this);
        }
    }

    public class MetafileNode
    {
        public string Text { get; set; }
        public List<string> Properties { get; private set; }
        public MetafileNode(string text)
        {
            Text = text;
            Properties = new List<string>();
        }
        public MetafileNode(string text, params string[] properties)
        {
            Text = text;
            Properties = new List<string>(properties);
        }
        public MetafileNode(string text, params object[] properties)
        {
            Text = text;
            Properties = new List<string>(properties.Select(o => o.ToString()));
        }
        public static implicit operator MetafileNode(string text)
        {
            return new MetafileNode(text);
        }
    }

    public class CompiledMetafile
    {
        public string Name { get; private set; }
        public Metafile Source { get; private set; }
        public uint Checksum { get; private set; }
        public byte[] Data { get; private set; }
        public CompiledMetafile(Metafile file)
        {
            Name = file.Name;
            Source = file;

            byte[] buffer;

            using (var metaFileStream = new MemoryStream())
            {
                using (var metaFileWriter = new BinaryWriter(metaFileStream, Encoding.GetEncoding(949), true))
                {
                    metaFileWriter.Write((byte)(file.Nodes.Count / 256));
                    metaFileWriter.Write((byte)(file.Nodes.Count % 256));
                    foreach (var node in file.Nodes)
                    {
                        buffer = Encoding.GetEncoding(949).GetBytes(node.Text);
                        metaFileWriter.Write((byte)buffer.Length);
                        metaFileWriter.Write(buffer);
                        metaFileWriter.Write((byte)(node.Properties.Count / 256));
                        metaFileWriter.Write((byte)(node.Properties.Count % 256));
                        foreach (var property in node.Properties)
                        {
                            buffer = Encoding.GetEncoding(949).GetBytes(property);
                            metaFileWriter.Write((byte)(buffer.Length / 256));
                            metaFileWriter.Write((byte)(buffer.Length % 256));
                            metaFileWriter.Write(buffer);
                        }
                    }
                }

                Checksum = ~Crc32.Calculate(metaFileStream.ToArray());

                using (var compressedMetaFileStream = new MemoryStream())
                {
                    ZlibCompression.Compress(metaFileStream, compressedMetaFileStream);
                    Data = compressedMetaFileStream.ToArray();
                }
            }
        }
    }
}
