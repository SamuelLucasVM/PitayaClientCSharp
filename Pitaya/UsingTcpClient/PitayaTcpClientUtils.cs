namespace Pitaya.NativeImpl {
    public static class Utils {
        public static void WriteBytes(byte[] bytes) {
            foreach (byte b in bytes) {
                Console.Write(b);
                Console.Write(" ");
            }
            Console.WriteLine("");
        }

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
                result = result<<8 + (int)v;
            }
            return result;
        }
    } 
}