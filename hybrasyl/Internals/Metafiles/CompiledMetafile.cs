// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System.IO;
using System.Text;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Compression;
using Hybrasyl.Internals.Crc;

namespace Hybrasyl.Internals.Metafiles;

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

    public byte[] Decompressed { get;  }
}