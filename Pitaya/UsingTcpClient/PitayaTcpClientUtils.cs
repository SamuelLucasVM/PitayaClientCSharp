using System;
using System.IO;
using System.Runtime.Serialization;

namespace Pitaya.NativeImpl {
    public static class Utils {
        public static byte[] IntToBytes(int n) {
            byte[] buf = new byte[3];
            buf[0] = (byte)((n >> 16) & 0xFF);
            buf[1] = (byte)((n >> 8) & 0xFF);
            buf[2] = (byte)(n & 0xFF);
            return buf;
        }

        public static int BytesToInt(byte[] b) {
            int result = 0;
            foreach (byte v in b) {
                result = (result<<8) + (int)v;
            }
            return result;
        }

        public static void NextMemoryStream(ref MemoryStream buf, int next) {
            buf.Seek(next, SeekOrigin.Begin);
            byte[] remainingBytes = new byte[buf.Length - next];
            buf.Read(remainingBytes, 0, remainingBytes.Length);
            buf = new MemoryStream(remainingBytes);
        }

        public static (int, uint) ParseHeader(byte[] header) {
            if (header.Length != PitayaGoToCSConstants.HeadLength) {
                throw new ErrInvalidPomeloHeader();
            }
            byte typ = header[0];
            if (typ < PitayaGoToCSConstants.Handshake || typ > PitayaGoToCSConstants.Kick) {
                throw new ErrWrongPomeloPacketType();
            }

            byte[] bytes = new byte[header.Length-1];
            Array.Copy(header, 1, bytes, 0, header.Length-1);
            int size = Utils.BytesToInt(bytes);

            if (size > PitayaGoToCSConstants.MaxPacketSize) {
                throw new ErrPacketSizeExcced();
            }

            return (size, typ);
        }
    } 
}