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

        public PitayaClientState State { get; set; } = PitayaClientState.Unknown;
        public int Quality { get; set; }
        private static NetworkStream _stream;
        private SessionHandshakeData _clientHandshake;
        private Channel<Packet> _packetChan;
        private Channel<Message> _incomingMsgChann;
        private Channel<bool> _pendingChan;
        private Dictionary<uint, PendingRequest> _pendingRequests;
        private uint _requestTimeout;
        private int _connTimeout;
        private bool Connected;
        
        public PitayaClient() : this(false) {}
        public PitayaClient(int connectionTimeout) : this(false, null, connectionTimeout: connectionTimeout) {}
        public PitayaClient(string certificateName = null) : this(false, certificateName: certificateName) {}
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
            SerializerFactory = serializerFactory;
            _eventManager = new EventManager();
            _client = new TcpClient();

            _clientHandshake = SessionHandshakeDataFactory.Default();

            _packetChan = Channel.CreateUnbounded<Packet>();
            _pendingChan = Channel.CreateBounded<bool>(30);
            _incomingMsgChann = Channel.CreateBounded<Message>(10);

            State = PitayaClientState.Inited;
            _pendingRequests = new Dictionary<uint, PendingRequest>();
            _requestTimeout = 5000; // 5 seconds
            _connTimeout = connTimeout;
            Quality = 0;

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

        // public static void SetLogLevel(PitayaLogLevel level)
        // {
        //     StaticPitayaBinding.SetLogLevel(level);
        // }

        // public static void SetLogFunction(NativeLogFunction fn)
        // {
        //     StaticPitayaBinding.SetLogFunction(fn);
        // }

        public void Connect(string host, int port, string handshakeOpts = null)
        {
            InternalConnect(host, port, handshakeOpts);
        }

        public void Connect(string host, int port, Dictionary<string, string> handshakeOpts)
        {
            var opts = Pitaya.SimpleJson.SimpleJson.SerializeObject(handshakeOpts);
            InternalConnect(host, port, opts);
        }

        async Task InternalConnect (string host, int port, string handshakeOpts) {
            State = PitayaClientState.Connecting;
            if (!string.IsNullOrEmpty(handshakeOpts)) {
                _clientHandshake = Pitaya.SimpleJson.SimpleJson.DeserializeObject<SessionHandshakeData>(handshakeOpts);
            }

            var connectTask = _client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(_connTimeout)) == connectTask) {
                State = PitayaClientState.Connected;
                _stream = _client.GetStream();
                HandleHandshake();
            } else {
                Console.WriteLine(string.Format("Connect Timeout: %1", _connTimeout));
            }
        }

        /// <summary cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)">
        /// </summary>
        public void Request<TResponse>(string route, object msg, Action<TResponse> action, Action<PitayaError> errorAction, int timeout = -1)
        {
            IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
            if (msg is IMessage) serializer = SerializerFactory.CreateProtobufSerializer(ProtobufSerializer.SerializationFormat.Protobuf);
            RequestInternal(route, msg, timeout, serializer, action, errorAction);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        public void Request(string route, Action<string> action, Action<PitayaError> errorAction)
        {
            Request(route, (string) null, action, errorAction);
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
            ProtobufSerializer.SerializationFormat format = ProtobufSerializer.SerializationFormat.Protobuf;
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

        async Task SendMsg(byte msgType, string route, byte[] data) {
            Message m = new Message {
                Type = msgType,
                Id = _reqUid,
                Route = route,
                Data = data,
                Err = false
            };

            byte[] byteMsg = BuildPacket(m);
            if (msgType == PitayaGoToCSConstants.Request) {
                // _pendingChan
                PendingRequest newRequest = new PendingRequest {
                    Msg = m,
                    SentAt = DateTime.Now.TimeOfDay,
                };
                _pendingRequests[m.Id] = newRequest;
            }
            
            await _stream.WriteAsync(byteMsg, 0, byteMsg.Length);
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
            if (msg is IMessage) serializer = SerializerFactory.CreateProtobufSerializer(ProtobufSerializer.SerializationFormat.Protobuf);
            NotifyInternal(route, msg, serializer, timeout);
        }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        public void Notify(string route, int timeout, IMessage msg)
        {
            ProtobufSerializer.SerializationFormat format = ProtobufSerializer.SerializationFormat.Protobuf;
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
            if (typeof(IMessage).IsAssignableFrom(typeof(T))) serializer = SerializerFactory.CreateProtobufSerializer(ProtobufSerializer.SerializationFormat.Protobuf);

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
        public void Disconnect() {
            if (Connected) {
                State = PitayaClientState.Disconnecting;
                Connected = false;
                _client.Close();
            }
        }

        //---------------Utils to TcpClient------------------------//

        byte[] BuildPacket(Message data) {
            byte[] encMsg = EncoderDecoder.EncodeMsg(data);

            return EncoderDecoder.EncodePacket(PitayaGoToCSConstants.Data, encMsg);
        }

        void HandleHandshake() {
            SendHandshakeRequest();
            HandleHandshakeResponse();
        }

        async Task HandleHandshakeResponse() {
            Packet[] packets = await ReadPackets();

            Packet handshakePacket = packets[0];
            if (handshakePacket.Type != PitayaGoToCSConstants.Handshake) {
                Console.WriteLine("got first packet from server that is not a handshake, aborting");
                return;
        	}

            HandshakeData handshake = new HandshakeData();
            if (Compression.IsCompressed(handshakePacket.Data)) {
                handshakePacket.Data = Compression.InflateData(handshakePacket.Data);
            }

            IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
            handshake = serializer.Decode<HandshakeData>(handshakePacket.Data);

            Console.WriteLine("got handshake from sv, data: " + handshake);
            byte[] p = EncoderDecoder.EncodePacket(PitayaGoToCSConstants.HandshakeAck, new byte[]{});
            await _stream.WriteAsync(p);

            Connected = true;   

            Thread sendHeartBeats = new Thread(() => SendHeartbeats((int)handshake.Sys.Heartbeat));
            Thread handleServerMessages = new Thread(() => HandleServerMessages());
            Thread handlePackets = new Thread(() => HandlePackets());
            Thread pendingRequestsReaper = new Thread(() => PendingRequestsReaper());

            sendHeartBeats.Start();
            handleServerMessages.Start();
            handlePackets.Start();
            pendingRequestsReaper.Start();
        }

        async Task HandlePackets(){
            while(await _packetChan.Reader.WaitToReadAsync()){
                while(_packetChan.Reader.TryRead(out Packet packet)){
                    switch(packet.Type){
                        case PitayaGoToCSConstants.Data:
                            Console.WriteLine("got data: " + System.Text.Encoding.UTF8.GetString(packet.Data));
                            Message message = EncoderDecoder.DecodeMsg(packet.Data);
                            if(message.Type == PitayaGoToCSConstants.Response){
                                if (_pendingRequests[message.Id] != null) {
                                    OnRequestResponse(message.Id, message.Data);
                                    _pendingRequests.Remove(message.Id);
                                }
                            }
                            await _incomingMsgChann.Writer.WriteAsync(message);
                            break;
                        case PitayaGoToCSConstants.Kick:
                            Console.WriteLine("got kick packet from the server! disconnecting...");
                            Disconnect();
                            break;
                    }
                }
            }
        }

        async Task HandleServerMessages() {
            try {
                while (Connected) {
                    Packet[] packets = await ReadPackets();

                    foreach (Packet p in packets) {
                        await _packetChan.Writer.WriteAsync(p);
                    }
                }
            } finally {
                Disconnect();
            }
        }

        async Task<Packet[]> ReadPackets() {
            // listen for sv messages
            MemoryStream buf = new MemoryStream();

            byte[] data = new byte[1024];
            int n = data.Length;

            while (n == data.Length) {
                n = await _stream.ReadAsync(data);
                buf.Write(data, 0, n);
            }
            
            Packet[] packets = null;
            packets = EncoderDecoder.DecodePacket(buf.ToArray());

            int totalProcessed = 0;
            foreach (Packet p in packets) {
                totalProcessed += PitayaGoToCSConstants.HeadLength + p.Length;
            }
            Utils.NextMemoryStream(ref buf, totalProcessed);

            return packets;
        }

        
        // pendingRequestsReaper delete timedout requests
        async Task PendingRequestsReaper() {
            while (true) {
                    List<PendingRequest> toDelete = new List<PendingRequest>();
                    foreach (PendingRequest v in _pendingRequests.Values) {
                        if ((uint)(DateTime.Now.TimeOfDay.TotalSeconds - v.SentAt.TotalSeconds) > _requestTimeout) {
                            toDelete.Add(v);
                        }
                    }

                    foreach (PendingRequest pendingReq in toDelete) {
                        // send a timeout to incoming msg chan
                        Message m = new Message {
                            Type = PitayaGoToCSConstants.Response,
                            Id = pendingReq.Msg.Id,
                            Route = pendingReq.Msg.Route,
                            Err = true,
                        };
                        _pendingRequests.Remove(m.Id);
                    }

                    await Task.Delay(1000);
            }
        }

        async Task SendHeartbeats(int interval) {
            try {
                while (true) {
                    byte[] p = EncoderDecoder.EncodePacket(PitayaGoToCSConstants.Heartbeat, new byte[] { });

                    int sentAt = (int) DateTime.Now.TimeOfDay.TotalSeconds;
                    await _stream.WriteAsync(p);

                    byte[] data = new byte[1024];
                    await _stream.ReadAsync(data);
                    int receivedAt = (int) DateTime.Now.TimeOfDay.TotalSeconds;

                    Quality = sentAt - receivedAt;

                    await Task.Delay(interval * 1000);
                }
            } finally {
                Disconnect();
            }
        }

        async Task SendHandshakeRequest() {
            string enc = Pitaya.SimpleJson.SimpleJson.SerializeObject(_clientHandshake);
            byte[] encBytes = Encoding.UTF8.GetBytes(enc);
            byte[] p = EncoderDecoder.EncodePacket(PitayaGoToCSConstants.Handshake, encBytes);

            await _stream.WriteAsync(p, 0, p.Length);
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
            Console.WriteLine(string.Format("PitayaClient Disposed {0}", _client));
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
