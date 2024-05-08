using System.Threading;
using System;
using System.Collections.Generic;

namespace Pitaya.NativeImpl
{
    public class PcMutex
    {
        private Mutex mutex;

        public PcMutex()
        {
            mutex = new Mutex();
        }

        public void Lock()
        {
            try
            {
                mutex.WaitOne();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error locking mutex: {ex.Message}");
                throw;
            }
        }

        public void Unlock()
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error unlocking mutex: {ex.Message}");
                throw;
            }
        }
    }

    public enum PcLocalStorageOperation
    {
        Read = 0,
        Write = 1
    }


    public delegate int PcLocalStorageCallback(PcLocalStorageOperation op, string data, ref long len, IntPtr exData);
    public class PcClientConfig
    {
        public int ConnTimeout { get; set; }

        public bool EnableReconn { get; set; }
        public int ReconnMaxRetry { get; set; }
        public int ReconnDelay { get; set; }
        public int ReconnDelayMax { get; set; }
        public int ReconnExpBackoff { get; set; }
        
        public bool EnablePolling { get; set; }
        
        public PcLocalStorageCallback LocalStorageCallback { get; set; }
        public IntPtr LsExData { get; set; } 
        
        public int TransportName { get; set; }
        public int DisableCompression { get; set; }
    }


    public delegate int InitDelegate(PcTransport trans, PcClient client);
    public delegate int ConnectDelegate(PcTransport trans, string host, int port, string handshakeOpt);
    public delegate int SendDelegate(PcTransport trans, string route, uint seqNum, PcBuffer buf, uint reqId, int timeout);
    public delegate int DisconnectDelegate(PcTransport trans);
    public delegate int CleanupDelegate(PcTransport trans);
    public delegate string SerializerDelegate(PcTransport trans);
    public delegate IntPtr InternalDataDelegate(PcTransport trans);
    public delegate int QualityDelegate(PcTransport trans);
    public delegate PcTransportPlugin PluginDelegate(PcTransport trans);
    public class PcTransport
    {
        public InitDelegate Init { get; set; }
        public ConnectDelegate Connect { get; set; }
        public SendDelegate Send { get; set; }
        public DisconnectDelegate Disconnect { get; set; }
        public CleanupDelegate Cleanup { get; set; }
        public SerializerDelegate Serializer { get; set; }
        
        public PluginDelegate Plugin { get; set; }
        // Optional fields
        public InternalDataDelegate InternalData { get; set; }
        public QualityDelegate Quality { get; set; }
    }


    public class PcBuffer
    {
        public byte[] Base { get; set; }
        public long Length { get; set; }
    }


    public delegate PcTransport TransportCreateDelegate(PcTransportPlugin plugin);
    public delegate void TransportReleaseDelegate(PcTransportPlugin plugin, PcTransport trans);
    public delegate void OnRegisterDelegate(PcTransportPlugin plugin);
    public delegate void OnDeregisterDelegate(PcTransportPlugin plugin);
    public class PcTransportPlugin
    {
        public TransportCreateDelegate TransportCreate { get; set; }
        public TransportReleaseDelegate TransportRelease { get; set; }

        // Optional fields
        public OnRegisterDelegate OnRegister { get; set; }
        public OnDeregisterDelegate OnDeregister { get; set; }

        public int TransportName { get; set; }

            // Constructor to initialize required properties
        public PcTransportPlugin(
            TransportCreateDelegate transportCreate,
            TransportReleaseDelegate transportRelease,
            int transportName,
            OnRegisterDelegate onRegister = null,
            OnDeregisterDelegate onDeregister = null)
        {
            TransportCreate = transportCreate;
            TransportRelease = transportRelease;
            OnRegister = onRegister;
            OnDeregister = onDeregister;
            TransportName = transportName;
        }
    }


    public delegate void PcRequestSuccessCallback(PcRequest req, PcBuffer resp);
    public delegate void PcRequestErrorCallback(PcRequest req, PcError error);
    public class PcRequest
    {
        public PcCommonRequest Base { get; set; }
        public uint RequestId { get; set; }
        public PcRequestSuccessCallback SuccessCallback { get; set; }
        public PcRequestErrorCallback ErrorCallback { get; set; }
    }


    public class PcEvent
    {
        public Queue<dynamic> Queue { get; set; }
        public uint Type { get; set; }
        public EventData Data { get; set; }
    }
    public abstract class EventData { }
    public class NotifyEventData : EventData
    {
        public uint SeqNum { get; set; }
        public PcError Error { get; set; }
    }
    public class RequestEventData : EventData
    {
        public uint ReqId { get; set; }
        public PcError Error { get; set; }
        public PcBuffer Resp { get; set; }
    }
    public class PushEventData : EventData
    {
        public string Route { get; set; }
        public PcBuffer Buf { get; set; }
    }
    public class EventEventData : EventData
    {
        public int EvType { get; set; }
        public string Arg1 { get; set; }
        public string Arg2 { get; set; }
    }
    public class PcError
    {
        public int Code { get; set; }
        public PcBuffer Payload { get; set; }
        public int UvCode { get; set; }
    }


    public class PcNotify
    {
        public PcCommonRequest Base { get; set; }
        public PcNotifyErrorCallback Callback { get; set; }
    }
    public delegate void PcNotifyErrorCallback(PcNotify req, PcError error);
    public class PcCommonRequest
    {
        public Queue<PcRequest> Queue { get; set; }
        public PcClient Client { get; set; }
        public uint Type { get; set; }
        public string Route { get; set; }
        public PcBuffer MessageBuffer { get; set; }
        public uint SeqNum { get; set; }
        public int Timeout { get; set; }
        public IntPtr ExData { get; set; }
    }

    public delegate void PcPushHandlerCallbackDelegate (PcClient client, string route, PcBuffer payload);
    public class PcClient
    {
        public PcMutex StateMutex { get; set; }
        public int State { get; set; }

        public PcClientConfig Config { get; set; }
        public IntPtr ExtraData { get; set; }

        public PcTransport Transport { get; set; }

        public PcMutex HandlerMutex { get; set; }
        public Queue<PcEventHandler> EventHandlers { get; set; }

        public PcMutex NotifyMutex { get; set; }
        public uint SequenceNumber { get; set; }
        public PcNotify[] Notifications { get; set; } = new PcNotify[PitayaNativeConstants.PC_PRE_ALLOC_NOTIFY_SLOT_COUNT];
        public Queue<PcNotify> NotificationQueue { get; set; }

        public PcPushHandlerCallbackDelegate PushHandler { get; set; }

        public PcMutex RequestMutex { get; set; }
        public uint RequestIdSequence { get; set; }
        public PcRequest[] Requests { get; set; } = new PcRequest[PitayaNativeConstants.PC_PRE_ALLOC_REQUEST_SLOT_COUNT];
        public Queue<PcRequest> RequestQueue { get; set; }

        public PcMutex EventMutex { get; set; }
        public PcEvent[] PendingEvents { get; set; } = new PcEvent[PitayaNativeConstants.PC_PRE_ALLOC_EVENT_SLOT_COUNT];
        public Queue<PcEvent> PendingEventQueue { get; set; } = new Queue<PcEvent>();
        public bool IsInPoll { get; set; }
    }

    public class PcClientInitResult {
        public PcClient Client;
        public int ReturnCode;
    }

    public delegate void PcLibLogDelegate(int level, string msg);
    public delegate void PcLibFreeDelegate(IntPtr data);
    public static class PcLogger {
        public static PcLibLogDelegate PcLibLog { get; set; }
        public static PcLibFreeDelegate PcLibFree { get; set; }
    }

    /**
    * pc_lib_init and pc_lib_cleanup both should be invoked only once.
    */
    public class PcLibClientInfo {
        public string Platform;
        public string BuildNumber;
        public string Version;
    }

    public delegate IntPtr PcAllocDelegate(int size);
    public delegate void PcFreeDelegate(IntPtr ptr);
    public delegate IntPtr PcReallocDelegate(IntPtr ptr, int size);

    public delegate void CustomAssertDelegate(string e, string file, int line);

    public delegate void CustomLogDelegate(int level, string format, params object[] args);

    public static class CustomLogger
    {
        private static CustomLogDelegate logFunction = null;
        public static int DefaultLogLevel = 0;

        public static void SetLogFunction(CustomLogDelegate func)
        {
            logFunction = func;
        }

        public static void Log(int level, string message, params object[] args) {
            if (logFunction == null) {
                DefaultLog(level, message, args);
            }
            else {
                logFunction(level, message, args);
            }
        }

        public static void DefaultLog(int level, string format, params object[] args)
        {
            if (logFunction == null)
                return;

            if (level < 0 || level < DefaultLogLevel)
                return;

            string timestamp = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]");
            string formattedMessage = string.Format(format, args);

            string logMessage = $"{timestamp} {formattedMessage}";

            switch (level) {
                case PitayaNativeConstants.PC_LOG_DEBUG:
                    Console.WriteLine("[DEBUG] ", logMessage);
                    break;
                case PitayaNativeConstants.PC_LOG_INFO:
                    Console.WriteLine("[INFO] ", logMessage);
                    break;
                case PitayaNativeConstants.PC_LOG_WARN:
                    Console.WriteLine("[WARN] ", logMessage);
                    break;
                case PitayaNativeConstants.PC_LOG_ERROR:
                    Console.WriteLine("[ERROR] ", logMessage);
                    break;
            }
        }
    }

    public class DummyTransport {
        public PcTransport Base;
        public PcClient Client;
    }

    public delegate void DestructorDelegate(IntPtr exData);
    public delegate void PcEventCallbackDelegate(PcClient client, int evType, IntPtr exData, string arg1, string arg2);
    public class PcEventHandler {
        public Queue<dynamic> Queue { get; set; }
        public IntPtr ExData { get; set; }
        public DestructorDelegate Destructor { get; set; }
        public int HandlerId { get; set; }
        public PcEventCallbackDelegate Callback { get; set; }
    }

    public delegate void RequestErrorCallback (PcClient client, uint cbid, PcError error);
    public delegate void RequestCallback (PcClient client, uint cbid, PcBuffer resp);
}