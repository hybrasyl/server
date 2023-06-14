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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hybrasyl.Interfaces;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl;


[MoonSharpUserData]
public class QuestMetadata
{
    public string Title; 
    public string Id;
    public int Circle = 0;
    public SortedSet<Class> AllowedClasses;
    public string Summary;
    public string Result;
    public string Prerequisite; // who knows
    public string Reward;

    // Client expects a string like "123", "12345" etc

    public QuestMetadata()
    {
        AllowedClasses = new SortedSet<Class> { Class.Monk, Class.Priest, Class.Wizard, Class.Rogue, Class.Warrior };
    }

    public void AddClass(Class c) => AllowedClasses.Add(c);

    public string Classes => AllowedClasses.Aggregate(string.Empty, (current, c) => current + (byte) c);

}

public class Metafile
{
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

    public string Name { get; set; }
    public List<MetafileNode> Nodes { get; }

    public CompiledMetafile Compile() => new(this);
}

public class MetafileNode
{
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
        Properties = new List<string>(properties.Select(selector: o => o.ToString()));
    }

    public string Text { get; set; }
    public List<string> Properties { get; }

    public static implicit operator MetafileNode(string text) => new(text);
}

public class CompiledMetafile : IStateStorable
{
    public CompiledMetafile(Metafile file)
    {
        Name = file.Name;
        Source = file;

        using (var metaFileStream = new MemoryStream())
        {
            using (var metaFileWriter =
                   new BinaryWriter(metaFileStream, CodePagesEncodingProvider.Instance.GetEncoding(949), true))
            {
                metaFileWriter.Write((byte) (file.Nodes.Count / 256));
                metaFileWriter.Write((byte) (file.Nodes.Count % 256));
                foreach (var node in file.Nodes)
                {
                    var nodeBuffer = CodePagesEncodingProvider.Instance.GetEncoding(949).GetBytes(node.Text);
                    metaFileWriter.Write((byte) nodeBuffer.Length);
                    metaFileWriter.Write(nodeBuffer);
                    metaFileWriter.Write((byte) (node.Properties.Count / 256));
                    metaFileWriter.Write((byte) (node.Properties.Count % 256));
                    foreach (var property in node.Properties)
                    {
                        var propertyBuffer = CodePagesEncodingProvider.Instance.GetEncoding(949).GetBytes(property);
                        metaFileWriter.Write((byte) (propertyBuffer.Length / 256));
                        metaFileWriter.Write((byte) (propertyBuffer.Length % 256));
                        metaFileWriter.Write(propertyBuffer);
                    }
                }
            }

            Checksum = ~Crc32.Calculate(metaFileStream.ToArray());
            metaFileStream.Seek(0, SeekOrigin.Begin);

            using (var compressedMetaFileStream = new MemoryStream())
            {
                ZlibCompression.Compress(metaFileStream, compressedMetaFileStream);
                Data = compressedMetaFileStream.ToArray();
            }
        }
    }

    public string Name { get; }
    public Metafile Source { get; }
    public uint Checksum { get; }
    public byte[] Data { get; }

    public byte[] Decompressed { get; private set; }
}