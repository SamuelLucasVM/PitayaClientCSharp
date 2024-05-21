using System;
using System.Collections.Generic;

namespace Pitaya.NativeImpl
{

    // HandshakeClientData represents information about the client sent on the handshake.
    public class HandshakeClientData
    {
        public string Platform;
        public string LibVersion;
        public string BuildNumber;
        public string Version;
    }

    // HandshakeData represents information about the handshake sent by the client.
    // `sys` corresponds to information independent from the app and `user` information
    // that depends on the app and is customized by the user.
    public class SessionHandshakeData
    {
        public HandshakeClientData Sys;
        public Dictionary<string, object> User;
    }

    public static class SessionHandshakeDataFactory
    {
        public static SessionHandshakeData Default()
        {
            SessionHandshakeData defaultHandShakeData = new SessionHandshakeData
            {
                Sys = new HandshakeClientData
                {
                    Platform = "mac",
                    LibVersion = "0.3.5-release",
                    BuildNumber = "20",
                    Version = "2.1",
                },
                User = new Dictionary<string, object>(),
            };
            defaultHandShakeData.User["age"] = 30;

            return defaultHandShakeData;
        }
    }

    public class HandshakeSys
    {
        public Dictionary<string, ushort> Dict;
        public int Heartbeat;
        public string Serializer;
    }

    public class HandshakeData
    {
        public int Code;
        public HandshakeSys Sys;
    }

    public class Packet
    {
        public uint Type;
        public int Length;
        public byte[] Data;
    }

    public class Message
    {
        public byte Type { get; set; }
        public uint Id { get; set; }
        public string Route { get; set; }
        public byte[] Data { get; set; }
        public bool Err { get; set; }
        public bool Compressed { get; set; }
    }

    public class PendingRequest
    {
        public Message Msg { get; set; }
        public TimeSpan SentAt { get; set; }
    }

    public static class RoutesCodesManager
    {
        public static readonly object routesCodesLock = new object();
        public static readonly Dictionary<string, ushort> routes = new Dictionary<string, ushort>();
        public static readonly Dictionary<ushort, string> codes = new Dictionary<ushort, string>();
    }
}