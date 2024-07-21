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
using System.IO.Compression;

namespace Hybrasyl.Internals.Compression;

public static class ZlibCompression
{
    public static void Compress(Stream originalStream, Stream compressedStream)
    {
        uint checksum = 0;

        using (var checksumStream = new MemoryStream())
        {
            var position = originalStream.Position;
            originalStream.CopyTo(checksumStream);
            originalStream.Seek(position, SeekOrigin.Begin);
            var buffer = checksumStream.ToArray();
            checksum = Adler32.ComputeHash(buffer);
        }

        compressedStream.Write(new byte[] { 0x78, 0x9C }, 0, 2);
        using (var compressionStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, true))
        {
            originalStream.Seek(0, SeekOrigin.Begin);
            originalStream.CopyTo(compressionStream);
        }

        compressedStream.Write(new[]
        {
            (byte) (checksum >> 24),
            (byte) (checksum >> 16),
            (byte) (checksum >> 8),
            (byte) checksum
        }, 0, 4);
    }

    public static void Decompress(Stream originalStream, Stream decompressedStream)
    {
        originalStream.Seek(2, SeekOrigin.Begin);
        using (var decompressionStream = new DeflateStream(originalStream, CompressionMode.Decompress, true))
        {
            decompressionStream.CopyTo(decompressedStream);
        }

        originalStream.Seek(4, SeekOrigin.Current);
    }
}