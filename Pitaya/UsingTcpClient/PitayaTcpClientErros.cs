using System;

namespace Pitaya.NativeImpl {
    [Serializable]
    public class ErrInvalidPomeloHeader : Exception {
        public ErrInvalidPomeloHeader() : base("invalid header") { }
        public ErrInvalidPomeloHeader(string message) : base(message) { }
        public ErrInvalidPomeloHeader(string message, Exception inner) : base(message, inner) { }
    
    }
    
    [Serializable]
    public class ErrWrongPomeloPacketType : Exception {
        public ErrWrongPomeloPacketType() : base("wrong packet type") { }
        public ErrWrongPomeloPacketType(string message) : base(message) { }
        public ErrWrongPomeloPacketType(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class ErrPacketSizeExcced : Exception {
        public ErrPacketSizeExcced() : base("codec: packet size exceed") { }
        public ErrPacketSizeExcced(string message) : base(message) { }
        public ErrPacketSizeExcced(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class ErrWrongMessageType : Exception {
        public ErrWrongMessageType() : base("wrong message type") { }
        public ErrWrongMessageType(string message) : base(message) { }
        public ErrWrongMessageType(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class ErrInvalidMessage : Exception {
        public ErrInvalidMessage() : base("invalid message") { }
        public ErrInvalidMessage(string message) : base(message) { }
        public ErrInvalidMessage(string message, Exception inner) : base(message, inner) { }
    }

    [Serializable]
    public class ErrRouteInfoNotFound : Exception {
        public ErrRouteInfoNotFound() : base("route info not found in dictionary") { }
        public ErrRouteInfoNotFound(string message) : base(message) { }
        public ErrRouteInfoNotFound(string message, Exception inner) : base(message, inner) { }
    }
}