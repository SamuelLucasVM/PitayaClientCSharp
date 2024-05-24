using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;

namespace Pitaya.NativeImpl
{

    // HandshakeClientData represents information about the client sent on the handshake.
    public class HandshakeClientData
    {
        [JsonProperty(PropertyName = "platform")]
        public string Platform;
        [JsonProperty(PropertyName = "libVersion")]
        public string LibVersion;
        [JsonProperty(PropertyName = "clientBuildNumber")]
        public string BuildNumber;
        [JsonProperty(PropertyName = "clientVersion")]
        public string Version;
    }

    // HandshakeData represents information about the handshake sent by the client.
    // `sys` corresponds to information independent from the app and `user` information
    // that depends on the app and is customized by the user.
    public class SessionHandshakeData
    {
        [JsonProperty(PropertyName = "sys")]
        public HandshakeClientData Sys { get; set; }
        [JsonProperty(PropertyName = "user")]
        public Dictionary<string, object> User { get; set; }
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

    public delegate void LogFunction(PitayaGoToCSConstants.LogLevel level, string message, params object[] args);

    public interface ILogger
    {
        void Debug(string message, params object[] args);
        void Info(string message, params object[] args);
        void Warn(string message, params object[] args);
        void Error(string message, params object[] args);
    }
    public class PitayaLogger : ILogger
    {
        private LogFunction _logFunction;

        public PitayaLogger(LogFunction newLogFunction)
        {
            _logFunction = newLogFunction ?? throw new ArgumentNullException(nameof(newLogFunction));
        }

        public void Debug(string message, params object[] args)
        {
            Log(PitayaGoToCSConstants.LogLevel.Debug, message, args);
        }

        public void Info(string message, params object[] args)
        {
            Log(PitayaGoToCSConstants.LogLevel.Info, message, args);
        }

        public void Warn(string message, params object[] args)
        {
            Log(PitayaGoToCSConstants.LogLevel.Warn, message, args);
        }

        public void Error(string message, params object[] args)
        {
            Log(PitayaGoToCSConstants.LogLevel.Error, message, args);
        }

        private void Log(PitayaGoToCSConstants.LogLevel level, string message, params object[] args)
        {
            if (_logFunction == null)
            {
                return;
            }

            if (level < 0 || level < PitayaClient.DefaultLogLevel)
            {
                return;
            }

            StringBuilder logBuf = new StringBuilder();

            DateTime currentTime = DateTime.Now;
            logBuf.Append(currentTime.ToString("[yyyy-MM-dd HH:mm:ss] "));

            string formattedMessage = string.Format(message, args);
            logBuf.Append(formattedMessage);

            _logFunction(level, logBuf.ToString());
        }
    }

}