using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using System.Net.Sockets;
using System.Text;
using Pitaya.NativeImpl;

using System.Threading.Tasks;
using System.Threading;
using System.Threading.Channels;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace Pitaya
{
    public class PitayaClient : IPitayaClient, IPitayaListener //IDisposable
    {
        public event Action<PitayaNetWorkState, NetworkError> NetWorkStateChangedEvent;

        public ISerializerFactory SerializerFactory { get; set; }


        private const int DEFAULT_CONNECTION_TIMEOUT = 30;
        private TcpClient _client = null;
        private EventManager _eventManager;
        private bool _disposed;
        private uint _reqUid;

        public PitayaClientState State { get; private set; } = PitayaClientState.Unknown;
        public int Quality { get; private set; }
        public static PitayaGoToCSConstants.LogLevel DefaultLogLevel { get; private set; } = PitayaGoToCSConstants.LogLevel.Debug;
        private PitayaLogger logger;
        private static Stream _stream;
        private object _readLock;
        private object _writeLock;
        private SessionHandshakeData _clientHandshake;
        private Channel<Packet> _packetChan;
        private Dictionary<uint, PendingRequest> _pendingRequests;
        private object _pendingRequestsLock;
        private uint _requestTimeout;
        private int _connTimeout;
        private bool Connected;
        private bool _enableTls;
        private string _certificateName;
        private bool _enableReconnect;
        private ProtobufSerializer.SerializationFormat _clientSerializer;

        public PitayaClient() : this(false) { }
        public PitayaClient(int connectionTimeout) : this(false, null, connectionTimeout: connectionTimeout) { }
        public PitayaClient(string certificateName = null) : this(false, certificateName: certificateName) { }
        public PitayaClient(bool enableReconnect = false, string certificateName = null, int connectionTimeout = DEFAULT_CONNECTION_TIMEOUT, IPitayaQueueDispatcher queueDispatcher = null, IPitayaBinding binding = null)
        {
            Init(certificateName, certificateName != null, false, enableReconnect, connectionTimeout, new SerializerFactory());
        }

        ~PitayaClient()
        {
            Dispose();
        }

        private void Init(string certificateName, bool enableTlS, bool enablePolling, bool enableReconnect, int connTimeout, ISerializerFactory serializerFactory)
        {
            SetLogFunction(DefaultLog);

            SerializerFactory = serializerFactory;
            _eventManager = new EventManager();
            _client = new TcpClient();
            
            _readLock = new object();
            _writeLock = new object();

            _clientHandshake = SessionHandshakeDataFactory.Default();

            _packetChan = Channel.CreateUnbounded<Packet>();
            State = PitayaClientState.Inited;
            Quality = 0;
            _pendingRequests = new Dictionary<uint, PendingRequest>();
            _pendingRequestsLock = new object();
            _requestTimeout = 5000; // 5 seconds
            _connTimeout = connTimeout;
            _enableTls = enableTlS;
            _certificateName = certificateName;
            _enableReconnect = enableReconnect;


            //             if (certificateName != null)
            //             {
            // #if UNITY_EDITOR
            //                 if (File.Exists(certificateName))
            //                     _binding.SetCertificatePath(certificateName);
            //                 else
            //                     _binding.SetCertificateName(certificateName);
            // #else
            //                 //_binding.SetCertificateName(certificateName);
            // #endif
            //             }
        }

        private void DefaultLog(PitayaGoToCSConstants.LogLevel level, string message, params object[] args)
        {
#if UNITY_EDITOR
                switch (level)
            {
                case PitayaGoToCSConstants.LogLevel.Debug:
                    Debug.Log("[DEBUG] " + message);
                    break;
                case PitayaGoToCSConstants.LogLevel.Info:
                    Debug.Log("[INFO] " + message);
                    break;
                case PitayaGoToCSConstants.LogLevel.Warn:
                    Debug.LogWarning("[WARN] " + message);
                    break;
                case PitayaGoToCSConstants.LogLevel.Error:
                    Debug.LogError("[ERROR] " + message);
                    break;
            }
#else
            switch (level)
            {
                case PitayaGoToCSConstants.LogLevel.Debug:
                    Console.Write("[DEBUG] ");
                    break;
                case PitayaGoToCSConstants.LogLevel.Info:
                    Console.Write("[INFO] ");
                    break;
                case PitayaGoToCSConstants.LogLevel.Warn:
                    Console.Write("[WARN] ");
                    break;
                case PitayaGoToCSConstants.LogLevel.Error:
                    Console.Write("[ERROR] ");
                    break;
            }

            Console.WriteLine(string.Format(message, args));
#endif
        }

        public static void SetLogLevel(PitayaGoToCSConstants.LogLevel level)
        {
            DefaultLogLevel = level;
        }

        public void SetLogFunction(LogFunction fn)
        {
            try
            {
                logger = new PitayaLogger(fn);
            }
            catch (ArgumentNullException e)
            {
                throw new Exception("Cannot initialize log function");
            }
        }

        public void Connect(string host, int port, string handshakeOpts = null)
        {
            InternalConnect(host, port, handshakeOpts);
        }

        public void Connect(string host, int port, Dictionary<string, string> handshakeOpts)
        {
            var opts = Pitaya.SimpleJson.SimpleJson.SerializeObject(handshakeOpts);
            InternalConnect(host, port, opts);
        }

        async Task InternalConnect(string host, int port, string handshakeOpts)
        {
            State = PitayaClientState.Connecting;
            if (!string.IsNullOrEmpty(handshakeOpts))
            {
                IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
                _clientHandshake = serializer.Decode<SessionHandshakeData>(Encoding.UTF8.GetBytes(handshakeOpts));
            }

            do {
                try {
                    Task connTask = _client.ConnectAsync(host, port);
                    if (await Task.WhenAny(connTask, Task.Delay(TimeSpan.FromSeconds(_connTimeout))) != connTask) {
                        throw new TimeoutException("connection timeout");
                    }

                    State = PitayaClientState.Connected;
                    _stream = _client.GetStream();

                    if (_enableTls) {
                        SslStream sslStream = new SslStream(_stream, false, new RemoteCertificateValidationCallback(Utils.ValidateServerCertificate), null);

                        // ---------------------------------------
                        X509Certificate2 certificate = new X509Certificate2(_certificateName);
                        // RSA rsaPrivateKey = LoadPrivateKeyFromPem(<key.pem-path>);

                        // certificate = certificate.CopyWithPrivateKey(rsaPrivateKey);

                        await sslStream.AuthenticateAsClientAsync(host, new  X509Certificate2Collection(certificate), System.Security.Authentication.SslProtocols.Tls12, false);
                        // ---------------------------------------

                        // await sslStream.AuthenticateAsClientAsync(host);

                        _stream = sslStream;
                    }

                    try {
                        await HandleHandshake();
                        return;
                    } catch (Exception e) {
                        Disconnect();
                        if (_enableReconnect && !_disposed) {
                            logger.Error(string.Format("error handling handshake: {0}, will reconn", e.ToString()));
                            _client = new TcpClient();
                            await Task.Delay(2000);
                        } else {
                            logger.Error(string.Format("error handling handshake: {0}", e.ToString()));
                        }
                    }
                }
                catch (Exception e) {
                    Disconnect();
                    if (_enableReconnect && !_disposed) {
                        logger.Error(string.Format("error connecting: {0}, will reconn", e.ToString()));
                        _client = new TcpClient();
                        await Task.Delay(2000);
                    }else {
                        logger.Error(string.Format("error connecting: {0}", e.ToString()));
                    }
                }
            } while (_enableReconnect);
        }

        /// <summary cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)">
        /// </summary>
        public void Request<TResponse>(string route, object msg, Action<TResponse> action, Action<PitayaError> errorAction, int timeout = -1)
        {
            IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
            if (msg is IMessage) serializer = SerializerFactory.CreateProtobufSerializer(_clientSerializer);
            RequestInternal(route, msg, timeout, serializer, action, errorAction);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        public void Request(string route, Action<string> action, Action<PitayaError> errorAction)
        {
            Request(route, (string)null, action, errorAction);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        public void Request<T>(string route, Action<T> action, Action<PitayaError> errorAction)
        {
            Request(route, null, action, errorAction);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        public void Request(string route, string msg, Action<string> action, Action<PitayaError> errorAction)
        {
            Request(route, msg, -1, action, errorAction);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        public void Request<T>(string route, IMessage msg, Action<T> action, Action<PitayaError> errorAction)
        {
            Request(route, msg, -1, action, errorAction);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        public void Request<T>(string route, IMessage msg, int timeout, Action<T> action, Action<PitayaError> errorAction)
        {
            ProtobufSerializer.SerializationFormat format = _clientSerializer;
            RequestInternal(route, msg, timeout, SerializerFactory.CreateProtobufSerializer(format), action, errorAction);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        public void Request(string route, string msg, int timeout, Action<string> action, Action<PitayaError> errorAction)
        {
            RequestInternal(route, msg, timeout, new LegacyJsonSerializer(), action, errorAction);
        }

        void RequestInternal<TResponse, TRequest>(string route, TRequest msg, int timeout, IPitayaSerializer serializer, Action<TResponse> action, Action<PitayaError> errorAction)
        {
            _reqUid++;

            Action<byte[]> responseAction = res => { action(serializer.Decode<TResponse>(res)); };

            _eventManager.AddCallBack(_reqUid, responseAction, errorAction);

            SendMsg(PitayaGoToCSConstants.Request, route, serializer.Encode(msg));
        }

        async Task SendMsg(byte msgType, string route, byte[] data)
        {
            Message m = new Message
            {
                Type = msgType,
                Id = _reqUid,
                Route = route,
                Data = data,
                Err = false
            };

            byte[] byteMsg = BuildPacket(m);
            if (msgType == PitayaGoToCSConstants.Request)
            {
                lock (_pendingRequestsLock)
                {
                    if (!_pendingRequests.ContainsKey(m.Id))
                    {
                        PendingRequest newRequest = new PendingRequest
                        {
                            Msg = m,
                            SentAt = DateTime.Now.TimeOfDay,
                        };
                        _pendingRequests[m.Id] = newRequest;
                    }
                }
            }

            lock (_writeLock) {
                _stream.Write(byteMsg, 0, byteMsg.Length);
            }
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        public void Notify(string route, IMessage msg)
        {
            Notify(route, -1, msg);
        }

        /// <summary cref="Notify(string, object, int)">
        /// </summary>
        public void Notify(string route, object msg, int timeout = -1)
        {
            IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
            if (msg is IMessage) serializer = SerializerFactory.CreateProtobufSerializer(_clientSerializer);
            NotifyInternal(route, msg, serializer, timeout);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        public void Notify(string route, int timeout, IMessage msg)
        {
            ProtobufSerializer.SerializationFormat format = _clientSerializer;
            NotifyInternal(route, msg, SerializerFactory.CreateProtobufSerializer(format), timeout);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        public void Notify(string route, string msg)
        {
            Notify(route, -1, msg);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        public void Notify(string route, int timeout, string msg)
        {
            byte[] bytes = new LegacyJsonSerializer().Encode(msg);
            SendMsg(PitayaGoToCSConstants.Notify, route, bytes);
        }

        void NotifyInternal(string route, object msg, IPitayaSerializer serializer, int timeout = -1)
        {
            SendMsg(PitayaGoToCSConstants.Notify, route, serializer.Encode(msg));
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="OnRoute&lt;T&gt;(string, Action&lt;T&gt;)"/> instead.</para>
        /// </summary>
        public void OnRoute(string route, Action<string> action)
        {
            OnRouteInternal(route, action, new LegacyJsonSerializer());
        }

        /// <summary cref="OnRoute&lt;T&gt;(string, Action&lt;T&gt;)">
        /// </summary>
        public void OnRoute<T>(string route, Action<T> action)
        {
            IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
            if (typeof(IMessage).IsAssignableFrom(typeof(T))) serializer = SerializerFactory.CreateProtobufSerializer(_clientSerializer);

            OnRouteInternal(route, action, serializer);
        }

        private void OnRouteInternal<T>(string route, Action<T> action, IPitayaSerializer serializer)
        {
            Action<byte[]> responseAction = res => { action(serializer.Decode<T>(res)); };
            _eventManager.AddOnRouteEvent(route, responseAction);
        }

        public void OffRoute(string route)
        {
            _eventManager.RemoveOnRouteEvent(route);
        }

        // Disconnect disconnects the client
        public void Disconnect()
        {
            if (Connected)
            {
                State = PitayaClientState.Disconnecting;
                Connected = false;
                _stream.Close();
                _client.Close();
            }
        }

        //---------------Utils to TcpClient------------------------//

        byte[] BuildPacket(Message data)
        {
            byte[] encMsg = EncoderDecoder.EncodeMsg(data);

            return EncoderDecoder.EncodePacket(PitayaGoToCSConstants.Data, encMsg);
        }

        async Task HandleHandshake()
        {
            await SendHandshakeRequest();
            await HandleHandshakeResponse();
        }

        async Task HandleHandshakeResponse()
        {
            Packet[] packets = await ReadPackets();

            Packet handshakePacket = packets[0];
            if (handshakePacket.Type != PitayaGoToCSConstants.Handshake)
            {
                logger.Error("got first packet from server that is not a handshake, aborting");
                return;
            }

            HandshakeData handshake = new HandshakeData();
            if (Compression.IsCompressed(handshakePacket.Data))
            {
                handshakePacket.Data = Compression.InflateData(handshakePacket.Data);
            }

            logger.Debug("got handshake from sv, data: {0}", System.Text.Encoding.UTF8.GetString(handshakePacket.Data));

            IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
            handshake = serializer.Decode<HandshakeData>(handshakePacket.Data);

            _clientSerializer = handshake.Sys.Serializer;

            if (handshake.Sys.Dict != null)
            {
                try
                {
                    Utils.SetDictionary(handshake.Sys.Dict);
                }
                catch
                {
                    // Intentionally left blank to ignore any exceptions just like Go code does
                }
            }

            byte[] p = EncoderDecoder.EncodePacket(PitayaGoToCSConstants.HandshakeAck, new byte[] { });
            lock (_writeLock) {
                _stream.Write(p);
            }

            Connected = true;

            Task sendHeartbeats = Task.Run(() => SendHeartbeats((int)handshake.Sys.Heartbeat));
            Task handleServerMessages = Task.Run(() => HandleServerMessages());
            Task handlePackets = Task.Run(() => HandlePackets());
            Task pendingRequestsReaper = Task.Run(() => PendingRequestsReaper());
        }

        async Task HandlePackets()
        {
            while (await _packetChan.Reader.WaitToReadAsync())
            {
                while (_packetChan.Reader.TryRead(out Packet packet))
                {
                    switch (packet.Type)
                    {
                        case PitayaGoToCSConstants.Data:
                            logger.Debug("got data: {0}", System.Text.Encoding.UTF8.GetString(packet.Data));

                            Message message;
                            try
                            {
                                message = EncoderDecoder.DecodeMsg(packet.Data);
                            }
                            catch (Exception e)
                            {
                                logger.Error("error decoding msg from sv: {0}", e.Message);
                                break;
                            }

                            if (message.Type == PitayaGoToCSConstants.Response)
                            {
                                if (_pendingRequests[message.Id] != null)
                                {
                                    OnRequestResponse(message.Id, message.Data);
                                    _pendingRequests.Remove(message.Id);
                                }
                            }
                            break;
                        case PitayaGoToCSConstants.Kick:
                            logger.Warn("got kick packet from the server! disconnecting...");
                            Disconnect();
                            break;
                    }
                }
            }
        }

        async Task HandleServerMessages()
        {
            try
            {
                while (Connected)
                {
                    Packet[] packets = null;
                    try
                    {
                        packets = await ReadPackets();
                    }
                    catch (Exception e)
                    {
                        logger.Error(string.Format("error handling server messages: {0}", e.ToString()));
                        break;
                    }

                    foreach (Packet p in packets)
                    {
                        await _packetChan.Writer.WriteAsync(p);
                    }
                }
            }
            finally
            {
                Disconnect();
            }
        }

        async Task<Packet[]> ReadPackets()
        {
            // listen for sv messages
            MemoryStream buf = new MemoryStream();

            byte[] data = new byte[1024];
            int n = data.Length;

            lock (_readLock) {
                while (n == data.Length)
                {
                    n = _stream.Read(data);
                    buf.Write(data, 0, n);
                }
            }

            Packet[] packets = { };
            try
            {
                packets = EncoderDecoder.DecodePacket(buf.ToArray());
            }
            catch (Exception e)
            {
                logger.Error(string.Format("error decoding packet from server: {0}", e.ToString()));
            }

            // is this Necessarily?
            int totalProcessed = 0;
            foreach (Packet p in packets)
            {
                totalProcessed += PitayaGoToCSConstants.HeadLength + p.Length;
            }
            Utils.NextMemoryStream(ref buf, totalProcessed);

            return packets;
        }


        // pendingRequestsReaper delete timedout requests
        async Task PendingRequestsReaper()
        {
            while (Connected)
            {
                List<PendingRequest> toDelete = new List<PendingRequest>();
                lock (_pendingRequestsLock)
                {
                    foreach (PendingRequest v in _pendingRequests.Values)
                    {
                        if ((uint)(DateTime.Now.TimeOfDay.TotalSeconds - v.SentAt.TotalSeconds) > _requestTimeout)
                        {
                            toDelete.Add(v);
                        }
                    }

                    foreach (PendingRequest pendingReq in toDelete)
                    {
                        CustomError err = ErrorHelper.Error(new Exception("request timeout"), "PIT-504");
                        byte[] errSerialized = Encoding.UTF8.GetBytes(Pitaya.SimpleJson.SimpleJson.SerializeObject(err));
                        // send a timeout to incoming msg chan
                        Message m = new Message
                        {
                            Type = PitayaGoToCSConstants.Response,
                            Id = pendingReq.Msg.Id,
                            Route = pendingReq.Msg.Route,
                            Data = errSerialized,
                            Err = true,
                        };
                        _pendingRequests.Remove(m.Id);
                    }
                }

                await Task.Delay(1000);
            }
        }

        async Task SendHeartbeats(int interval)
        {
            try
            {
                while (Connected)
                {
                    byte[] p = EncoderDecoder.EncodePacket(PitayaGoToCSConstants.Heartbeat, new byte[] { });

                    int sentAt = (int)DateTime.Now.TimeOfDay.TotalSeconds;

                    lock (_writeLock) {
                        try
                        {
                            _stream.Write(p);
                        }
                        catch (Exception e)
                        {
                            logger.Error(string.Format("error sending heartbeat to server: {0}", e.ToString()));
                            return;
                        }
                    }

                    lock (_readLock) {
                        byte[] data = new byte[1024];
                        _stream.Read(data);
                    }
                    int receivedAt = (int)DateTime.Now.TimeOfDay.TotalSeconds;

                    Quality = sentAt - receivedAt;

                    await Task.Delay(interval * 1000);
                }
            }
            finally
            {
                Disconnect();
            }
        }

        async Task SendHandshakeRequest()
        {
            lock (_writeLock) {
                IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
                byte[] encBytes = serializer.Encode(_clientHandshake);
                byte[] p = EncoderDecoder.EncodePacket(PitayaGoToCSConstants.Handshake, encBytes);

                _stream.Write(p, 0, p.Length);
            }
        }

        RSA LoadPrivateKeyFromPem(string pemPath)
        {
            using (var reader = File.OpenText(pemPath))
            {
                PemReader pemReader = new PemReader(reader);
                var keyPair = (AsymmetricCipherKeyPair)pemReader.ReadObject();
                var rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)keyPair.Private);
                RSA rsa = RSA.Create();
                rsa.ImportParameters(rsaParams);
                return rsa;
            }
        }

        //---------------Pitaya Listener------------------------//

        public void OnRequestResponse(uint rid, byte[] data)
        {
            _eventManager.InvokeCallBack(rid, data);
        }

        public void OnRequestError(uint rid, PitayaError error)
        {
            _eventManager.InvokeErrorCallBack(rid, error);
        }

        public void OnNetworkEvent(PitayaNetWorkState state, NetworkError error)
        {
            if (NetWorkStateChangedEvent != null) NetWorkStateChangedEvent.Invoke(state, error);
        }

        public void OnUserDefinedPush(string route, byte[] serializedBody)
        {
            _eventManager.InvokeOnEvent(route, serializedBody);
        }

        public void Dispose()
        {
            logger.Debug("PitayaClient Disposed {0}", _client);
            if (_disposed)
                return;

            if (_eventManager != null) _eventManager.Dispose();

            _reqUid = 0;
            _client.Close();

            _client = null;
            _disposed = true;
        }

        public void ClearAllCallbacks()
        {
            _eventManager.ClearAllCallbacks();
        }

        public void RemoveAllOnRouteEvents()
        {
            _eventManager.RemoveAllOnRouteEvents();
        }
    }
}
