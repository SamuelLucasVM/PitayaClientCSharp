using System;
using System.IO;
using System.IO.Compression;

namespace Pitaya.NativeImpl {
    public class Compression {
        public static bool IsCompressed(byte[] data) {
            return data.Length > 2 &&
            (
                (data[0] == 0x78 &&
                (data[1] == 0x9C ||
                data[1] == 0x01 ||
                data[1] == 0xDA ||
                data[1] == 0x5E)) ||
                (data[0] == 0x1F && data[1] == 0x8B));
        }

        public static byte[] InflateData(byte[] data) {

            using (var ms = new MemoryStream(data))
            using (var output = new MemoryStream())
            using (var deflateStream = new DeflateStream(ms, CompressionMode.Decompress, true))
            {
                if (ms.ReadByte() != 0x78 || ms.ReadByte() != 0x9C) {
                    throw new Exception("Incorrect zlib header");
                }

                deflateStream.CopyTo(output);
                return output.ToArray();
            }
        }

    }
}