using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Pitaya.NativeImpl
{
    public static class StaticPitayaBindingCS 
    {
        public static PcTransportPlugin[] PcTransportPluginRepo = new PcTransportPlugin[PitayaNativeConstants.PC_TRANSPORT_PLUGIN_SLOT_COUNT];
        static int PcDefaultLogLevel = 0;
        static int PcInitiateded = 0;
        public static CustomAssertDelegate Assert = null;
        public static CustomLogDelegate PcLibLog;
        public static string PcLibPlatformStr = null;
        public static string PcLibClientBuildNumberStr = null;
        public static string PcLibClientVersionStr = null;

        // public static int TrUvTlsSetCaFile(string caFile, string caPath)
        // {
        //     if (instance.ctx) {
        //         int ret = SSL_CTX_load_verify_locations(instance.ctx, ca_file, ca_path);
        //         if (!ret) {
        //             this.PcLibLog(PC_LOG_WARN, "tr_uv_tls_set_ca_file - load verify locations error, cafile: "+ caFile + ", capath: " + caPath);
        //             return PC_RC_ERROR;
        //         }
        //         return PC_RC_OK;
        //     } else {
        //         return PC_RC_ERROR;
        //     }
        // }

        public static void PcClientSetPushHandler(ref PcClient client, PcPushHandlerCallbackDelegate cb)
        {
            client.PushHandler = cb;
        }

        public static int PcClientAddEvHandler(PcClient client, PcEventCallbackDelegate cb, IntPtr exData, DestructorDelegate destructor)
        {
            PcEventHandler handler = new PcEventHandler();
            int handlerId = 0;

            if (client == null || cb== null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_add_ev_handler - invalid args");
                return PitayaNativeConstants.PC_EV_INVALID_HANDLER_ID;
            }

            handler.Queue = new Queue<dynamic>();
            handler.ExData = exData;
            handler.Callback = cb;
            handler.HandlerId = handlerId++;
            handler.Destructor = destructor;

            // pc_mutex_lock(client->handler_mutex);

            // QUEUE_INSERT_TAIL(&client->ev_handlers, &handler->queue);
            client.EventHandlers.Enqueue(handler);
            PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc_client_add_ev_handler -" +
                    " add event handler, handler id: " + handler.HandlerId);

            // pc_mutex_unlock(&client->handler_mutex);

            return handler.HandlerId;
        }

        public static void PcUnityLibInit(int logLevel, string caFile, string caPath, CustomAssertDelegate customAssert, string platform, string buildNumber, string version){
            if (customAssert != null) {
                Assert = customAssert;
            }

            PcLibClientInfo clientInfo = new PcLibClientInfo{
                Platform = platform,
                BuildNumber = buildNumber,
                Version = version
            };

            PcDefaultLogLevel = logLevel;

            PcLibInit(CustomLogger.DefaultLog, null, null, null, clientInfo);

            #if (!PC_NO_UV_TLS_TRANS)
                if (caFile != null || caPath != null) {
                    // TrUvTlsSetCaFile(caFile, caPath);
                }
            #endif
        }

        public static string PcLibStrdup(string str) {
            if (str == null)
                return null;

            int len = str.Length;
            char[] buf = new char[len + 1];
            str.CopyTo(0, buf, 0, len);
            buf[len] = '\0';

            return new string(buf);
        }

        public static int PcTransportPluginRegister(PcTransportPlugin plugin) {
            int transName;
            if (plugin == null || plugin.TransportName >= PitayaNativeConstants.PC_TRANSPORT_PLUGIN_SLOT_COUNT
                || plugin.TransportName < 0 || plugin.TransportCreate == null || plugin.TransportRelease == null)
                return PitayaNativeConstants.PC_RC_INVALID_ARG;

            transName = plugin.TransportName;
            if (PcTransportPluginRepo[transName] != null)
                PcTransportPluginDeregister(transName);

            PcTransportPluginRepo[transName] = plugin;

            if (plugin.OnRegister != null)
                plugin.OnRegister(plugin);

            return PitayaNativeConstants.PC_RC_OK;
        }

        public static int PcTransportPluginDeregister(int transName)
        {
            PcTransportPlugin tp;
            if (transName >= PitayaNativeConstants.PC_TRANSPORT_PLUGIN_SLOT_COUNT || transName < 0)
                return PitayaNativeConstants.PC_RC_INVALID_ARG;

            tp = PcTransportPluginRepo[transName];

            if (tp != null && tp.OnDeregister != null)
                tp.OnDeregister(tp);

            PcTransportPluginRepo[transName] = null;

            return PitayaNativeConstants.PC_RC_OK;
        }

        public static void PcLibInit(CustomLogDelegate pcLog, PcAllocDelegate pcAlloc, PcFreeDelegate pcFree, PcReallocDelegate pcRealloc, PcLibClientInfo clientInfo) {
            if(PcInitiateded == 1) {
                return; // init function already called
            }
            PcInitiateded = 1;

            // pc_mutex_init(&pc__pinned_keys_mutex);
            // pc_lib_clear_pinned_public_keys();

            PcTransportPlugin tp;

            PcLibLog = pcLog != null ? pcLog : CustomLogger.DefaultLog;

            PcLibPlatformStr = clientInfo.Platform != null 
                ? PcLibStrdup(clientInfo.Platform) 
                : PcLibStrdup("desktop");
            PcLibClientBuildNumberStr = clientInfo.BuildNumber != null 
                ? PcLibStrdup(clientInfo.BuildNumber)
                : PcLibStrdup("1");
            PcLibClientVersionStr = clientInfo.Version != null 
                ? PcLibStrdup(clientInfo.Version)
                : PcLibStrdup("0.1");

        // #if PC_NO_DUMMY_TRANS
            tp = StaticDummyTransport.PcTrDummyTransPlugin();
            PcTransportPluginRegister(tp);
            PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc_lib_init - register dummy plugin");
        // #endif

        // #if PC_NO_UV_TCP_TRANS
        //     tp = pc_tr_uv_tcp_trans_plugin();
        //     pc_transport_plugin_register(tp);
        //     PcLibLog(PC_LOG_INFO, "pc_lib_init - register tcp plugin");
        // #if PC_NO_UV_TLS_TRANS
        //     tp = pc_tr_uv_tls_trans_plugin();
        //     pc_transport_plugin_register(tp);
        //     PcLibLog(PC_LOG_INFO, "pc_lib_init - register tls plugin");
        // #endif
        //     srand((unsigned int)time(0));
        // #endif
        }

        public static PcClient PcUnityCreate(bool enableTls, bool enablePoll, bool enableReconnect, int connTimeout) {
            // Assert(connTimeout >= 0);

            PcClientInitResult res = new PcClientInitResult{ReturnCode = 0};
            PcClientConfig config = new PcClientConfig
            {
                ConnTimeout = 30,
                EnableReconn = true,
                ReconnMaxRetry = PitayaNativeConstants.PC_ALWAYS_RETRY,
                ReconnDelay = 2,
                ReconnDelayMax = 30,
                ReconnExpBackoff = 1,
                EnablePolling = false,
                TransportName = PitayaNativeConstants.PC_TR_NAME_UV_TCP,
                DisableCompression = 0,
            };

            if (enableTls) {
                config.TransportName = PitayaNativeConstants.PC_TR_NAME_UV_TLS;
            };

            config.EnablePolling = enablePoll;
            config.EnableReconn = enableReconnect;
            config.ConnTimeout = connTimeout;

            res = PcClientInit(IntPtr.Zero, config);
            if (res.ReturnCode == PitayaNativeConstants.PC_RC_OK) {
                return res.Client;
            }

            return null;
        } 

        private static PcTransportPlugin PcGetTransportPlugin(int transName)
        {
            if (transName >= PitayaNativeConstants.PC_TRANSPORT_PLUGIN_SLOT_COUNT || transName < 0)
                return null;

            return PcTransportPluginRepo[transName];
        }

        private static PcClientInitResult PcClientInit(IntPtr exData, PcClientConfig config)
        {
            PcClientInitResult res = new PcClientInitResult{ReturnCode = 0};

            res.ReturnCode = PitayaNativeConstants.PC_RC_ERROR;
            res.Client = new PcClient();

            if (config == null) {
                res.Client.Config = new PcClientConfig
                {
                ConnTimeout = 30,
                EnableReconn = true,
                ReconnMaxRetry = PitayaNativeConstants.PC_ALWAYS_RETRY,
                ReconnDelay = 2,
                ReconnDelayMax = 30,
                ReconnExpBackoff = 1,
                EnablePolling = false,
                TransportName = PitayaNativeConstants.PC_TR_NAME_UV_TCP,
                DisableCompression = 0,
                };
            } else {
                res.Client.Config = config;
            }

            // ::TODO:: Not working with res.Client.Config.TransportName
            
            // PcTransportPlugin tp = PcGetTransportPlugin(res.Client.Config.TransportName);
            PcTransportPlugin tp = PcGetTransportPlugin(7);
            if (tp == null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_init - no registered transport plugin found, transport plugin: " + config.TransportName);
                // IntPtr clientPtr = GetObjectPointer(res.Client);
                // PcLogger.PcLibFree(clientPtr);
                res.Client = null;
                res.ReturnCode = PitayaNativeConstants.PC_RC_NO_TRANS;
                return res;
            }

            // Assert(tp.TransportCreate != null);
            // Assert(tp.TransportRelease != null);

            PcTransport trans = tp.TransportCreate(tp);
            if (trans == null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_init - create transport error");
                // IntPtr clientPtr = GetObjectPointer(res.Client);
                // PcLogger.PcLibFree(clientPtr);
                res.Client = null;
                res.ReturnCode = PitayaNativeConstants.PC_RC_ERROR;
                return res;
            }

            res.Client.Transport = trans;

            // Assert(res.Client.Transport.Init != null);

            if (res.Client.Transport.Init(res.Client.Transport, res.Client) == null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_init - init transport error");
                tp.TransportRelease(tp, trans);
                // pc_lib_free(res.Client);
                res.Client = null;
                res.ReturnCode = PitayaNativeConstants.PC_RC_ERROR;
                return res;
            }

            // res.Client.StateMutex = new PcMutex();

            res.Client.ExtraData = exData;
            
            // res.Client.HandlerMutex = new PcMutex();
            res.Client.EventHandlers = new Queue<PcEventHandler>();

            // res.Client.RequestMutex = new PcMutex();
            // res.Client.NotifyMutex = new PcMutex();

            res.Client.RequestQueue = new Queue<PcRequest>();
            res.Client.NotificationQueue = new Queue<PcNotify>();

            res.Client.SequenceNumber = 0;
            res.Client.RequestIdSequence = 1;

            // memset(&res.client->requests[0], 0, sizeof(pc_request_t) * PC_PRE_ALLOC_REQUEST_SLOT_COUNT);
            // memset(&res.client->notifies[0], 0, sizeof(pc_notify_t) * PC_PRE_ALLOC_NOTIFY_SLOT_COUNT);

            for (int i = 0; i < PitayaNativeConstants.PC_PRE_ALLOC_REQUEST_SLOT_COUNT; i++) {
                res.Client.Requests[i] = new PcRequest();
                res.Client.Requests[i].Base = new PcCommonRequest();

                res.Client.Requests[i].Base.Queue = new Queue<PcRequest>();
                res.Client.Requests[i].Base.Client = res.Client;
                res.Client.Requests[i].Base.Type = PitayaNativeConstants.PC_REQ_TYPE_REQUEST | PitayaNativeConstants.PC_PRE_ALLOC_ST_IDLE | PitayaNativeConstants.PC_PRE_ALLOC;
            }

            for (int i = 0; i < PitayaNativeConstants.PC_PRE_ALLOC_NOTIFY_SLOT_COUNT; i++) {
                res.Client.Notifications[i] = new PcNotify();
                res.Client.Notifications[i].Base = new PcCommonRequest();

                res.Client.Notifications[i].Base.Queue = new Queue<PcRequest>();
                res.Client.Notifications[i].Base.Client = res.Client;
                res.Client.Notifications[i].Base.Type = PitayaNativeConstants.PC_REQ_TYPE_NOTIFY | PitayaNativeConstants.PC_PRE_ALLOC_ST_IDLE | PitayaNativeConstants.PC_PRE_ALLOC;
            }

            res.Client.EventMutex = new PcMutex();
            if (res.Client.Config.EnablePolling) {
                res.Client.PendingEventQueue = new Queue<PcEvent>();

                // memset(&res.client->pending_events[0], 0, sizeof(pc_event_t) * PC_PRE_ALLOC_EVENT_SLOT_COUNT);

                for (int i = 0; i < PitayaNativeConstants.PC_PRE_ALLOC_EVENT_SLOT_COUNT; i++) {
                    PcEvent pendingEvent = new PcEvent();
                    res.Client.PendingEvents[i].Queue = new Queue<dynamic>();
                    res.Client.PendingEvents[i].Type = PitayaNativeConstants.PC_PRE_ALLOC_ST_IDLE | PitayaNativeConstants.PC_PRE_ALLOC;
                }
            }

            res.Client.IsInPoll = false;
            res.Client.State = PitayaNativeConstants.PC_ST_INITED;
            res.ReturnCode = PitayaNativeConstants.PC_RC_OK;

            PcLibLog(PitayaNativeConstants.PC_LOG_DEBUG, "pc_client_init - init ok");
            return res;
        }

        public static int PcClientState(PcClient client)
        {
            int state;

            if (client == null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_DEBUG, "pc_client_state - client is null");
                return PitayaNativeConstants.PC_ST_UNKNOWN;
            }

            // pc_mutex_lock(&client->state_mutex);
            state = client.State;
            // pc_mutex_unlock(&client->state_mutex);

            Console.WriteLine(state);
            return state;
        }

        public static string PcClientRcStr(int rc) {
            // pc_assert(rc <= 0 && rc > PC_RC_MIN);
            return PitayaNativeConstants.RcStrings[-rc];
        }

        public static string PcClientStateStr(int state) {
            // pc_assert(state < PC_ST_COUNT && state >= 0);
            return PitayaNativeConstants.StateStrings[state];
        }

        public static int PcClientConnect(PcClient client, string host, int port, string handshakeOpts)
        {
            int state;
            int ret;

            if (client == null || host == null || port < 0 || port > (1 << 16) - 1) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_connect - invalid args");
                return PitayaNativeConstants.PC_RC_INVALID_ARG;
            }

            if (client.Config.EnablePolling) {
                PcClientPoll(client);
            }

            state = PcClientState(client);
            switch(state) {
                case PitayaNativeConstants.PC_ST_DISCONNECTING:
                    PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_connect - invalid state, state: " + PcClientStateStr(state));
                    return PitayaNativeConstants.PC_RC_INVALID_STATE;

                case PitayaNativeConstants.PC_ST_CONNECTED:
                case PitayaNativeConstants.PC_ST_CONNECTING:
                    PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc_client_connect - client already connecting or connected");
                    return PitayaNativeConstants.PC_RC_OK;

                case PitayaNativeConstants.PC_ST_INITED:
                    // pc_assert(client->trans && client->trans->connect);

                    // pc_mutex_lock(&client->state_mutex);
                    client.State = PitayaNativeConstants.PC_ST_CONNECTING; 
                    // pc_mutex_unlock(&client->state_mutex);

                    ret = client.Transport.Connect(client.Transport, host, port, handshakeOpts);

                    if (ret != PitayaNativeConstants.PC_RC_OK) {
                        PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_connect - transport connect error, rc: " + PcClientStateStr(ret));
                        // pc_mutex_lock(&client->state_mutex);
                        client.State = PitayaNativeConstants.PC_ST_INITED;
                        // pc_mutex_unlock(&client->state_mutex);
                    }

                    return ret;
                }
            PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_connect - unknown client state found, state: " + state);
            return PitayaNativeConstants.PC_RC_ERROR;
        }

        public static int PcClientPoll(PcClient client)
        {
            PcEvent ev = new PcEvent();
            Queue<dynamic> q = new Queue<dynamic>();

            if (client == null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_poll - client is null");
                return PitayaNativeConstants.PC_RC_INVALID_ARG;
            }

            if (client.Config.EnablePolling) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_poll - client did not enable polling");
                return PitayaNativeConstants.PC_RC_ERROR;
            }

            // pc_mutex_lock(&client->event_mutex);

            /*
            * `is_in_poll` is used to avoid recursive invocation of pc_client_poll
            * by identical thread as `pc_mutex_t` is recursive.
            *
            * `is_in_poll` can be protected by `event_mutex` too, so no extra mutex
            * is needed here.
            */
            if (!client.IsInPoll) {
                client.IsInPoll = true;

                while(client.PendingEventQueue.Count != 0) {
                    ev = client.PendingEventQueue.Dequeue();

                    // pc_assert((PC_IS_PRE_ALLOC(ev->type) && PC_PRE_ALLOC_IS_BUSY(ev->type)) || PC_IS_DYN_ALLOC(ev->type));

                    PcHandleEvent(client, ev);
                }
                client.IsInPoll = false;
            }

            // pc_mutex_unlock(&client->event_mutex);

            return PitayaNativeConstants.PC_RC_OK;
        }

        // public static bool IsResp(int type)
        // {
        //     return (type & PitayaNativeConstants.PC_EV_TYPE_MASK) == PitayaNativeConstants.PC_EV_TYPE_RESP;
        // }

        // public static bool IsNotifySent(int type) {
        //     return (type & PitayaNativeConstants.PC_EV_TYPE_MASK == PitayaNativeConstants.PC_EV_TYPE_NOTIFY_SENT);
        // }

        // public static bool IsPush(int type) {
        //     return (type & PitayaNativeConstants.PC_EV_TYPE_MASK == PitayaNativeConstants.PC_EV_TYPE_PUSH);
        // }

        public static void PreAllocSetIdle(ref uint type)
        {
            type &= ~PitayaNativeConstants.PC_PRE_ALLOC_ST_MASK;
            type |= PitayaNativeConstants.PC_PRE_ALLOC_ST_IDLE;
        }

        public static bool PcIsDynAlloc(uint type) {
            return (type & PitayaNativeConstants.PC_ALLOC_MASK) == PitayaNativeConstants.PC_DYN_ALLOC;
        }

        private static void PcHandleEvent(PcClient client, PcEvent ev)
        {
            // pc_assert(PC_EV_IS_RESP(ev->type) || PC_EV_IS_NOTIFY_SENT(ev->type) || PC_EV_IS_NET_EVENT(ev->type));

            if (ev.Data is RequestEventData requestEnvData) {
                StaticPcTrans.PcTransResp(client, requestEnvData.ReqId, requestEnvData.Resp, requestEnvData.Error);
                PcLibLog(PitayaNativeConstants.PC_LOG_DEBUG, "pc__handle_event - fire pending trans resp, req_id: " + requestEnvData.ReqId);

                // pc__error_free(&ev->data.req.error);

                // pc_lib_free((char* )ev->data.req.resp.base);
                requestEnvData.Resp.Base = null;
                requestEnvData.Resp.Length = -1;

            } else if (ev.Data is NotifyEventData notifyEventData) {
                StaticPcTrans.PcTransSent(client, notifyEventData.SeqNum, notifyEventData.Error);
                PcLibLog(PitayaNativeConstants.PC_LOG_DEBUG, "pc__handle_event - fire pending trans sent, seq_num: " + notifyEventData.SeqNum + ", rc: " + notifyEventData.Error.Code);

                // pc__error_free(&ev->data.notify.error);
            } else if (ev.Data is PushEventData pushEventData) {
                StaticPcTrans.PcTransPush(client, pushEventData.Route, pushEventData.Buf);

                // PcLibLog(PitayaNativeConstants.PC_LOG_DEBUG, "pc__handle_event - fire pending trans sent, seq_num: " + ev.Data.Notify.SeqNum + ", rc: " + ev.Data.Notify.Error.Code);

                // pc_lib_free((char*)ev->data.push.route);
            } else if (ev.Data is EventEventData eventEventData) {
                StaticPcTrans.PcTransFireEvent(client, eventEventData.EvType, eventEventData.Arg1, eventEventData.Arg2);
                PcLibLog(PitayaNativeConstants.PC_LOG_DEBUG, "pc__handle_event - fire pending trans event: " + StaticPcTrans.PcClientEvStr(eventEventData.EvType) + ", arg1: " + eventEventData.Arg1 != null ? eventEventData.Arg1 : "");
                // pc_lib_free((char* )ev->data.ev.arg1);
                // pc_lib_free((char* )ev->data.ev.arg2);

                eventEventData.Arg1 = null;
                eventEventData.Arg2 = null;
            }

            if (PcIsDynAlloc(ev.Type)) {
                // pc_lib_free(ev);
            } else {
                uint eventType = ev.Type;
                PreAllocSetIdle(ref eventType);
                ev.Type = eventType;
            }
        }

        public static int PcClientConnQuality(PcClient client)
        {
            if (client == null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_conn_quality - client is null, invalid arg");
                return PitayaNativeConstants.PC_RC_INVALID_ARG;
            }

            // pc_assert(client->trans);

            if (client.Transport.Quality != null) {
                return client.Transport.Quality(client.Transport);
            } else {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_conn_quality - transport doesn't support qulity query");
            }

            return PitayaNativeConstants.PC_RC_ERROR;
        }

        public static string PcClientSerializer(PcClient client)
        {
            if (client == null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_serializer - client is null, invalid arg");
                return null;
            }

            // pc_assert(client->trans);
            return client.Transport.Serializer(client.Transport);
        }

        public static int PcClientDisconnect(PcClient client)
        {
            int state;
            int ret;

            if (client == null) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_disconnect - client is null");
                return PitayaNativeConstants.PC_RC_INVALID_ARG;
            }

            if (client.Config.EnablePolling) {
                PcClientPoll(client);
            }

            state = PcClientState(client);
            switch(state) {
                case PitayaNativeConstants.PC_ST_INITED:
                    PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_disconnect - invalid state, state: " + PcClientStateStr(state));
                    return PitayaNativeConstants.PC_RC_INVALID_STATE;

                case PitayaNativeConstants.PC_ST_CONNECTING:
                case PitayaNativeConstants.PC_ST_CONNECTED:
                    // pc_assert(client->trans && client->trans->disconnect);

                    // pc_mutex_lock(&client->state_mutex);
                    client.State = PitayaNativeConstants.PC_ST_DISCONNECTING;
                    // pc_mutex_unlock(&client->state_mutex);

                    ret = client.Transport.Disconnect(client.Transport);

                    if (ret != PitayaNativeConstants.PC_RC_OK) {
                        PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_disconnect - transport disconnect error: " + PcClientRcStr(ret));
                        // pc_mutex_lock(&client->state_mutex);
                        client.State = state;
                        // pc_mutex_unlock(&client->state_mutex);
                    }
                    return ret;

                case PitayaNativeConstants.PC_ST_DISCONNECTING:
                    PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc_client_disconnect - client is already disconnecting");
                    return PitayaNativeConstants.PC_RC_OK;
            }
            PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_disconnect - unknown client state found, " + state);
            return PitayaNativeConstants.PC_RC_ERROR;
        }

        public static int PcBinaryNotifyWithTimeout(PcClient client, string route, byte[] data, long len, IntPtr exData, int timeout, PcNotifyErrorCallback cb)
        {
            PcBuffer buf = new PcBuffer();
            buf.Length = len;
            // buf.Base = pc_lib_malloc(len);
            buf.Base = data;
            return PcNotifyWithTimeout(client, route, buf, exData, timeout, cb);
        }

        public static int PcNotifyWithTimeout(PcClient client, string route, PcBuffer msgBuf, IntPtr exData,
                                        int timeout, PcNotifyErrorCallback cb)
        {
            PcNotify notify;
            int i;
            int ret;
            int state;

            if (client == null || route == null || msgBuf.Length == -1) {
                // pc_assert(msgBuf.base == NULL);
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_notify_with_timeout - invalid args");
                // pc_buf_free(&msgBuf);
                return PitayaNativeConstants.PC_RC_INVALID_ARG;
            }

            if (timeout != PitayaNativeConstants.PC_WITHOUT_TIMEOUT && timeout <= 0) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_notify_with_timeout - invalid timeout value");
                // pc_buf_free(&msgBuf);
                return PitayaNativeConstants.PC_RC_INVALID_ARG;
            }

            state = PcClientState(client);
            if(state != PitayaNativeConstants.PC_ST_CONNECTED && state != PitayaNativeConstants.PC_ST_CONNECTING) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_request_with_timeout - invalid state, state: " + PcClientStateStr(state));
                // pc_buf_free(&msgBuf);
                return PitayaNativeConstants.PC_RC_INVALID_STATE;
            }

            // pc_assert(client->trans && client->trans->send);

            // pc_mutex_lock(&client->req_mutex);

            notify = null;
            for (i = 0; i < PitayaNativeConstants.PC_PRE_ALLOC_NOTIFY_SLOT_COUNT; i++) {
                if (StaticPcTrans.IsIdle(client.Notifications[i].Base.Type)) {
                    notify = client.Notifications[i];

                    uint type = notify.Base.Type;
                    StaticPcTrans.SetBusy(ref type);
                    notify.Base.Type = type;

                    PcLibLog(PitayaNativeConstants.PC_LOG_DEBUG, "pc_notify_with_timeout - use pre alloc notify");
                    // pc_assert(!notify->base.route && !notify->base.msgBuf.base);
                    // pc_assert(PC_IS_PRE_ALLOC(notify->base.type));

                    break;
                }
            }

            if (notify == null) {
                notify = new PcNotify();

                PcLibLog(PitayaNativeConstants.PC_LOG_DEBUG, "pc_notify_with_timeout - use dynamic alloc notify");
                notify.Base.Type = PitayaNativeConstants.PC_REQ_TYPE_NOTIFY | PitayaNativeConstants.PC_DYN_ALLOC;
                notify.Base.Client = client;
            }

            // QUEUE_INIT(&notify->base.queue);
            // notify.Base.Queue = new Queue<PcRequest>();
            // // QUEUE_INSERT_TAIL(&client->notify_queue, &notify->base.queue);
            // notify.Base.Queue.Enqueue(client.NotificationQueue);

            notify.Base.Route = PcLibStrdup(route);
            notify.Base.MessageBuffer = msgBuf;

            notify.Base.SeqNum = client.SequenceNumber++;

            notify.Base.Timeout = timeout;
            notify.Base.ExData = exData;

            notify.Callback = cb;

            // pc_mutex_unlock(&client->req_mutex);

            PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc_notify_with_timeout - add notify to queue, seq num: " + notify.Base.SeqNum);

            ret = client.Transport.Send(client.Transport, notify.Base.Route, notify.Base.SeqNum, 
                notify.Base.MessageBuffer, PitayaNativeConstants.PC_NOTIFY_PUSH_REQ_ID, notify.Base.Timeout);

            if (ret != PitayaNativeConstants.PC_RC_OK) {
                PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_notify_with_timeout - send to transport error, seq num: " + notify.Base.SeqNum + ", error: " + PcClientRcStr(ret));

                // pc_mutex_lock(&client->req_mutex);

                // pc_buf_free(&notify->base.msgBuf);
                // pc_lib_free((char* )notify->base.route);

                notify.Base.MessageBuffer.Base = null;
                notify.Base.MessageBuffer.Length = -1;
                notify.Base.Route = null;

                notify.Base.Queue.Dequeue();

                if (StaticPcTrans.IsPreAlloc(notify.Base.Type)) {
                    uint type = notify.Base.Type;
                    PreAllocSetIdle(ref type);
                    notify.Base.Type = notify.Base.Type;
                } else {
                    // pc_lib_free(notify);
                }

                // pc_mutex_unlock(&client->req_mutex);
            }
            return ret;
        }
    }
}