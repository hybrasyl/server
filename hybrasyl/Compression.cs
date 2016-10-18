using System.IO;
using System.IO.Compression;

namespace Hybrasyl
{
    public static class ZlibCompression
    {
        public static void Compress(Stream originalStream, Stream compressedStream)
        {
            compressedStream.Write(new byte[] { 0x78, 0x9C }, 0, 2);
            using (var compressionStream = new DeflateStream(compressedStream, CompressionMode.Compress, true))
            {
                originalStream.Seek(0, SeekOrigin.Begin);
                originalStream.CopyTo(compressionStream);
            }
        }

        public static void Decompress(Stream originalStream, Stream decompressedStream)
        {
            originalStream.Seek(2, SeekOrigin.Begin);
            using (var decompressionStream = new DeflateStream(originalStream, CompressionMode.Decompress, true))
            {
                decompressionStream.CopyTo(decompressedStream);
            }
        }
    }
}
