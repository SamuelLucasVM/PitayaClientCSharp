using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Text;
using Pitaya.NativeImpl;

namespace Pitaya
{
    public class PitayaClient : IPitayaClient, IPitayaListener //IDisposable
    {
        public event Action<PitayaNetWorkState, NetworkError> NetWorkStateChangedEvent;

        public ISerializerFactory SerializerFactory { get; set; }
        
        private const int DEFAULT_CONNECTION_TIMEOUT = 30;

        private TcpClient _client = null;
        private static NetworkStream _stream = null;
        private object _clientHandshake = null;
        private bool DataCompression = false;
        private EventManager _eventManager;
        private bool _disposed;
        private uint _reqUid;
        private Dictionary<uint, Action<string, string>> _requestHandlers;
        private IPitayaBinding _binding = new PitayaBinding();
        
        public PitayaClient() : this(false) {}
        public PitayaClient(int connectionTimeout) : this(false, null, connectionTimeout: connectionTimeout) {}
        public PitayaClient(string certificateName = null) : this(false, certificateName: certificateName) {}
        public PitayaClient(bool enableReconnect = false, string certificateName = null, int connectionTimeout = DEFAULT_CONNECTION_TIMEOUT, IPitayaQueueDispatcher queueDispatcher = null, IPitayaBinding binding = null)
        {
            _client = new TcpClient();

            _clientHandshake = new {
			    sys = new {
                    platform = "mac",
                    libVersion = "0.3.5-release",
                    buildNumber = "20",
                    version = "2.1",
			    },
                user = new {
                    age = "30",
                },
		    };
        }

        ~PitayaClient()
        {
            // Dispose();
        }

//         private void Init(string certificateName, bool enableTlS, bool enablePolling, bool enableReconnect, int connTimeout, ISerializerFactory serializerFactory)
//         {
//             SerializerFactory = serializerFactory;
//             _eventManager = new EventManager();
//             _client = _binding.CreateClient(enableTlS, enablePolling, enableReconnect, connTimeout, this);

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
//         }

        public static void SetLogLevel(PitayaLogLevel level)
        {
            StaticPitayaBinding.SetLogLevel(level);
        }

        public static void SetLogFunction(NativeLogFunction fn)
        {
            StaticPitayaBinding.SetLogFunction(fn);
        }

        // public int Quality
        // {
        //     get { return _binding.Quality(_client); }
        // }


        // public PitayaClientState State
        // {
        //     get { return _binding.State(_client); }
        // }

        public void Close() {
            _client.Close();
        }

        private byte[] BuildPacket(Request data) {
            byte[] encMsg = EncoderDecoder.EncodeMsg(data);

            return EncoderDecoder.EncodePacket(PitayaGoToCSConstants.Data, encMsg);
        }

        private void HandleHandshake() {
            SendHandshakeRequest();
            HandleHandshakeResponse();
        }

        private void HandleHandshakeResponse() {
            // MemoryStream buf = new MemoryStream();
            // Packet[] packets = ReadPackets(buf);

            // Packet handshakePacket = packets[0];
            // if (handshakePacket.Type != PitayaGoToCSConstants.Handshake) {
            //     return;
            // }

            // HandshakeData handshake = new HandshakeData();
            // if compression.IsCompressed(handshakePacket.Data) {
            //     handshakePacket.Data, err = compression.InflateData(handshakePacket.Data)
            //     if err != nil {
            //         return err
            //     }
            // }

            // err = json.Unmarshal(handshakePacket.Data, handshake)

            // Console.WriteLine("got handshake from sv, data: " + handshake);

            // if handshake.Sys.Dict != nil {
            //     message.SetDictionary(handshake.Sys.Dict)
            // }
            byte[] p = EncoderDecoder.EncodePacket(PitayaGoToCSConstants.HandshakeAck, new byte[]{});
            _stream.Write(p);

            // c.Connected = true

            // go c.sendHeartbeats(handshake.Sys.Heartbeat)
            // go c.handleServerMessages()
            // go c.handlePackets()
            // go c.pendingRequestsReaper()

        }

        private void SendHandshakeRequest() {
            string enc = Newtonsoft.Json.JsonConvert.SerializeObject(_clientHandshake);
            byte[] encBytes = Encoding.UTF8.GetBytes(enc);
            byte[] p = EncoderDecoder.EncodePacket(PitayaGoToCSConstants.Handshake, encBytes);

            _stream.Write(p, 0, p.Length);
        }

        public void Connect(string host, int port)
        {
            _client.Connect(host, port);
            _stream = _client.GetStream();

            HandleHandshake();
        }

        public void SendRequest(string route, byte[] data) {
            Request request = new Request(){
                Type = PitayaGoToCSConstants.Request,
                Id = 1,
                Route = route,
                Data = data,
                Err = false
            };

            byte[] byteRequest = BuildPacket(request);
            
            Utils.WriteBytes(byteRequest);
            _stream.Write(byteRequest, 0, byteRequest.Length);
        }

        private static Packet[] ReadPackets(MemoryStream buf) {
            // listen for sv messages
            byte[] data = new byte[1024];
            int n = data.Length;

            while (n == data.Length) {
                n = _stream.Read(data);
                buf.Write(data, 0, n);
            }
            Packet[] packets = EncoderDecoder.DecodePacket(buf.ToArray());

            int totalProcessed = 0;
            foreach (Packet p in packets) {
                totalProcessed += PitayaGoToCSConstants.HeadLength + p.Length;
            }
            // buf.Next(totalProcessed)
            buf.Seek(totalProcessed, SeekOrigin.Begin);

            return packets;
        }

        // public void Connect(string host, int port, Dictionary<string, string> handshakeOpts)
        // {
        //     var opts = Pitaya.SimpleJson.SimpleJson.SerializeObject(handshakeOpts);
        //     _binding.Connect(_client, host, port, opts);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        // public void Request(string route, Action<string> action, Action<PitayaError> errorAction)
        // {
        //     Request(route, (string) null, action, errorAction);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        // public void Request<T>(string route, Action<T> action, Action<PitayaError> errorAction)
        // {
        //     Request(route, null, action, errorAction);
        // }

        /// <summary cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)">
        /// </summary>
        // public void Request<TResponse>(string route, object msg, Action<TResponse> action, Action<PitayaError> errorAction, int timeout = -1)
        // {
        //     IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
        //     if (msg is IMessage) serializer = SerializerFactory.CreateProtobufSerializer(_binding.ClientSerializer(_client));
        //     RequestInternal(route, msg, timeout, serializer, action, errorAction);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        // public void Request(string route, string msg, Action<string> action, Action<PitayaError> errorAction)
        // {
        //     Request(route, msg, -1, action, errorAction);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        // public void Request<T>(string route, IMessage msg, Action<T> action, Action<PitayaError> errorAction)
        // {
        //     Request(route, msg, -1, action, errorAction);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        // public void Request<T>(string route, IMessage msg, int timeout, Action<T> action, Action<PitayaError> errorAction)
        // {
        //     ProtobufSerializer.SerializationFormat format = _binding.ClientSerializer(_client);
        //     RequestInternal(route, msg, timeout, SerializerFactory.CreateProtobufSerializer(format), action, errorAction);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Request&lt;TResponse&gt;(string, object, Action&lt;TResponse&gt;, Action&lt;PitayaError&gt;, int)"/> instead.</para>
        /// </summary>
        // public void Request(string route, string msg, int timeout, Action<string> action, Action<PitayaError> errorAction)
        // {
        //     RequestInternal(route, msg, timeout, new LegacyJsonSerializer(), action, errorAction);
        // }

        // void RequestInternal<TResponse, TRequest>(string route, TRequest msg, int timeout, IPitayaSerializer serializer, Action<TResponse> action, Action<PitayaError> errorAction)
        // {
        //     _reqUid++;

        //     Action<byte[]> responseAction = res => { action(serializer.Decode<TResponse>(res)); };

        //     _eventManager.AddCallBack(_reqUid, responseAction, errorAction);

        // _binding.Request(_client, route, serializer.Encode(msg), _reqUid, timeout);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        // public void Notify(string route, IMessage msg)
        // {
        //     Notify(route, -1, msg);
        // }

        /// <summary cref="Notify(string, object, int)">
        /// </summary>
        // public void Notify(string route, object msg, int timeout = -1)
        // {
        //     IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
        //     if (msg is IMessage) serializer = SerializerFactory.CreateProtobufSerializer(_binding.ClientSerializer(_client));
        //     NotifyInternal(route, msg, serializer, timeout);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        // public void Notify(string route, int timeout, IMessage msg)
        // {
        //     ProtobufSerializer.SerializationFormat format = _binding.ClientSerializer(_client);
        //     NotifyInternal(route, msg, SerializerFactory.CreateProtobufSerializer(format), timeout);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        // public void Notify(string route, string msg)
        // {
        //     Notify(route, -1, msg);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="Notify(string, object, int)"/> instead.</para>
        /// </summary>
        // public void Notify(string route, int timeout, string msg)
        // {
        //     byte[] bytes = new LegacyJsonSerializer().Encode(msg);
        //     _binding.Notify(_client, route, bytes, timeout);
        // }

        // private void NotifyInternal(string route, object msg, IPitayaSerializer serializer, int timeout = -1)
        // {
        //     _binding.Notify(_client, route, serializer.Encode(msg), timeout);
        // }

        /// <summary>
        /// <para>DEPRECATED. Use <see cref="OnRoute&lt;T&gt;(string, Action&lt;T&gt;)"/> instead.</para>
        /// </summary>
        // public void OnRoute(string route, Action<string> action)
        // {
        //     OnRouteInternal(route, action, new LegacyJsonSerializer());
        // }

        /// <summary cref="OnRoute&lt;T&gt;(string, Action&lt;T&gt;)">
        /// </summary>
        // public void OnRoute<T>(string route, Action<T> action)
        // {
        //     IPitayaSerializer serializer = SerializerFactory.CreateJsonSerializer();
        //     if (typeof(IMessage).IsAssignableFrom(typeof(T))) serializer = SerializerFactory.CreateProtobufSerializer(_binding.ClientSerializer(_client));

        //     OnRouteInternal(route, action, serializer);
        // }

        private void OnRouteInternal<T>(string route, Action<T> action, IPitayaSerializer serializer)
        {
            Action<byte[]> responseAction = res => { action(serializer.Decode<T>(res)); };
            _eventManager.AddOnRouteEvent(route, responseAction);
        }

        public void OffRoute(string route)
        {
            _eventManager.RemoveOnRouteEvent(route);
        }

        // public void Disconnect()
        // {
        //     _binding.Disconnect(_client);
        // }

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

        // public void Dispose()
        // {
        //     Debug.Log(string.Format("PitayaClient Disposed {0}", _client));
        //     if (_disposed)
        //         return;

        //     if (_eventManager != null) _eventManager.Dispose();

        //     _reqUid = 0;
        //     _binding.Disconnect(_client);
        //     _binding.Dispose(_client);

        //     _client = IntPtr.Zero;
        //     _disposed = true;
        // }

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