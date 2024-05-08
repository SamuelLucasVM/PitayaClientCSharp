namespace Pitaya.NativeImpl {
    public class HandshakeSys {
        public Dictionary<string, ushort> Dict;
        public int Heartbeat;
        public string Serializer;
    }

    public class HandshakeData {
        public int Code;
        public HandshakeSys Sys;
    }

    public class Packet {
        public uint Type;
        public int Length;
        public byte[] Data;
    }

    public class Request {
        public uint Type { get; set; }
        public uint Id { get; set; }
        public string Route { get; set; }
        public byte[] Data { get; set; }
        public bool Err { get; set; }
    }

}