namespace Pitaya.NativeImpl
{
    public static class PitayaGoToCSConstants {
        // Errors
        public const int HeadLength = 4;
        public const int MaxPacketSize = 1 << 24; //16MB

        // Packet
        public const uint Handshake = 0x01;
        public const uint HandshakeAck = 0x02;
        public const uint Heartbeat = 0x03;
        public const uint Data = 0x04;
        public const uint Kick = 0x05;

        // Message Types
        public const byte Request = 0x00;
        public const byte Notify = 0x01;
        public const byte Response = 0x02;
        public const byte Push = 0x03;
    }
}