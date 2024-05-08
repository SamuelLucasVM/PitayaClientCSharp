using System.Collections.Generic;

namespace Pitaya.NativeImpl {
    public static class StaticPcTrans {
        public static bool IsIdle(uint eventType) {
            return (eventType & PitayaNativeConstants.PC_PRE_ALLOC_ST_MASK) == PitayaNativeConstants.PC_PRE_ALLOC_ST_IDLE;
        }

        public static void SetBusy(ref uint type)
        {
            type &= ~PitayaNativeConstants.PC_PRE_ALLOC_ST_MASK; 
            type |= PitayaNativeConstants.PC_PRE_ALLOC_ST_BUSY;  
        }

        public static void SetNetEvent(ref uint type)
        {
            type &= ~PitayaNativeConstants.PC_EV_TYPE_MASK;
            type |= PitayaNativeConstants.PC_EV_TYPE_NET_EVENT;
        }

        public static void SetNotifySent(ref uint type)
        {
            type &= ~PitayaNativeConstants.PC_EV_TYPE_MASK;
            type |= PitayaNativeConstants.PC_EV_TYPE_NOTIFY_SENT;
        }

        public static void SetResponseEvent(ref uint type)
        {
            type &= ~PitayaNativeConstants.PC_EV_TYPE_MASK;
            type |= PitayaNativeConstants.PC_EV_TYPE_RESP;
        }

        public static PcBuffer PcBufferCopy(PcBuffer buf)
        {
            if (buf.Base == null) {
                PcBuffer emptyBuff = new PcBuffer();
                return emptyBuff;
            }

            // StaticPitayaBindingCS.Assert(buf.Base != null);
            // StaticPitayaBindingCS.Assert(buf.Length >= 0);

            PcBuffer newBuf = new PcBuffer();
            newBuf.Base = new byte[buf.Length];
            newBuf.Length = buf.Length;

            if (newBuf.Base == null) {
                newBuf.Length = -1;
                return newBuf;
            }

            newBuf.Base = buf.Base;

            newBuf.Length = buf.Length;
            return newBuf;
        }

        public static PcError PcErrorDup(PcError err)
        {
            // pc_error_t new_err = {0};
            // new_err.code = err->code;
            // if (err->payload.base) {
            //     new_err.payload = pc_buf_copy(&err->payload);
            // }
            // return new_err;
            
            PcError newErr = new PcError
            {
                Code = err.Code,
                Payload = new PcBuffer()
            };

            if (err.Payload.Base != null)
            {
                newErr.Payload = PcBufferCopy(err.Payload);
            }

            return newErr;
        }

        public static bool IsPreAlloc(uint type)
        {
            return (type & PitayaNativeConstants.PC_ALLOC_MASK) == PitayaNativeConstants.PC_PRE_ALLOC;
        }

        public static void SetPreAllocIdle(ref uint type)
        {
            type &= ~PitayaNativeConstants.PC_PRE_ALLOC_ST_MASK;
            type |= PitayaNativeConstants.PC_PRE_ALLOC_ST_IDLE;
        }

        public static string PcClientEvStr(int evType) {
            // StaticPitayaBindingCS.Assert(evType >= 0 && evType < PitayaNativeConstants.PC_EV_COUNT);
            return PitayaNativeConstants.EvStrings[evType];
        }

        public static void PcTransQueueResp(PcClient client, uint reqId, PcBuffer resp, PcError error)
        {
            // pc_mutex_lock(&client->event_mutex);

            StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_queue_resp - add pending resp event, req_id: %u", reqId);

            PcEvent ev = null;
            uint eventType = 0;
            for (int i = 0; i < PitayaNativeConstants.PC_PRE_ALLOC_EVENT_SLOT_COUNT; i++) {
                if (IsIdle(client.PendingEvents[i].Type)) {
                    ev = client.PendingEvents[i];
                    eventType = ev.Type;
                    SetBusy(ref eventType);
                    ev.Type = eventType;
                    break;
                }
            }

            if (ev == null) {
                // ev = (pc_event_t* )pc_lib_malloc(sizeof(pc_event_t));
                // memset(ev, 0, sizeof(pc_event_t));

                ev.Type = PitayaNativeConstants.PC_DYN_ALLOC;
                eventType = PitayaNativeConstants.PC_DYN_ALLOC;
            }

            SetResponseEvent(ref eventType);
            ev.Type = eventType;

            ev.Queue = new Queue<dynamic>();
            if (ev.Data is RequestEventData requestEventData) {
                requestEventData.ReqId = reqId;
                requestEventData.Resp = PcBufferCopy(resp);
                requestEventData.Error = PcErrorDup(error);
            }


            // QUEUE_INSERT_TAIL(&client->pending_ev_queue, &ev->queue);
            client.PendingEventQueue.Enqueue(ev);

            // pc_mutex_unlock(&client->event_mutex);
        }

        public static void PcTransResp(PcClient client, uint reqId, PcBuffer resp, PcError error)
        {
            if (client == null) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_trans_resp - client is null");
                return ;
            }

            if (client.Config.EnablePolling) {
                PcTransQueueResp(client, reqId, resp, error);
            } else {
                PcTransResp2(client, reqId, resp, error);
            }
        }

        public static void PcTransResp2(PcClient client, uint reqId, PcBuffer resp, PcError error)
        {
            Queue<dynamic> q = new Queue<dynamic>();

            /* invoke callback immediately */
            PcRequest target = new PcRequest();
            uint requestType = 0;
            // pc_mutex_lock(&client->req_mutex);
            // QUEUE_FOREACH(q, &client->req_queue) {
            foreach (PcRequest req in client.RequestQueue) {
                if (req.RequestId == reqId) {
                    if (error != null) {
                        StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_resp - fire resp event, req_id: " + reqId + ", error: " + error.Code);
                    } else {
                        StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_resp - fire resp event, req_id: " + reqId);
                    }

                    target = req;
                    client.RequestQueue.Dequeue();
                    break;
                }
            }

            // pc_mutex_unlock(&client->req_mutex);

            if (target != null) {
                if (error != null && target.ErrorCallback != null) {
                    target.ErrorCallback(target, error);
                } else if (error == null) {
                    target.SuccessCallback(target, resp);
                }

                // pc_buf_free(&target->base.msg_buf);
                // pc_lib_free((char*)target->base.route);

                target.Base.Route = null;

                if (IsPreAlloc(target.Base.Type)) {
                    // pc_mutex_lock(&client->req_mutex);
                    requestType = target.Base.Type;
                    SetPreAllocIdle(ref requestType);
                    target.Base.Type = requestType;
                    // pc_mutex_unlock(&client->req_mutex);

                } else {
                    // pc_lib_free(target);
                }
            } else {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__trans_resp - no pending request found when get a response, req id: " + reqId);
            }
        }

        public static void PcTransSent(PcClient client, uint seqNum, PcError error)
        {
            if (client == null) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_trans_sent - client is null");
                return ;
            }

            if (client.Config.EnablePolling) {
                PcTransQueueSent(client, seqNum, error);
            } else {
                PcTransSent2(client, seqNum, error);
            }
        }

        public static void PcTransQueueSent(PcClient client, uint seqNum, PcError error)
        {
            PcEvent ev;
            int i;

            // pc_mutex_lock(&client->event_mutex);

            if (error != null) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_queue_sent - add pending sent event, seq_num: " + seqNum + ", rc: " + error.Code);
            } else {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_queue_sent - add pending sent event, seq_num: " + seqNum);
            }

            ev = null;
            uint eventType = 0;
            for (i = 0; i < PitayaNativeConstants.PC_PRE_ALLOC_EVENT_SLOT_COUNT; i++) {
                if (IsIdle(client.PendingEvents[i].Type)) {
                    ev = client.PendingEvents[i];
                    eventType = ev.Type;
                    SetBusy(ref eventType);
                    ev.Type = eventType;
                    break;
                }
            }

            if (ev == null) {
                // ev = (pc_event_t* )pc_lib_malloc(sizeof(pc_event_t));
                // memset(ev, 0, sizeof(pc_event_t));
                ev.Type = PitayaNativeConstants.PC_DYN_ALLOC;
                eventType = PitayaNativeConstants.PC_DYN_ALLOC;
            }

            ev.Queue = new Queue<dynamic>();

            SetNotifySent(ref eventType);
            ev.Type = eventType;

            if (ev.Data is NotifyEventData notifyEventData) {
                notifyEventData.SeqNum = seqNum;
                notifyEventData.Error = PcErrorDup(error);
            }

            client.PendingEventQueue.Enqueue(ev);

            // pc_mutex_unlock(&client->event_mutex);
        }

        public static void PcTransSent2(PcClient client, uint seqNum, PcError error)
        {
            Queue<dynamic> q = new Queue<dynamic>();
            PcNotify target = null;
            uint notifyType = 0;

            /* callback immediately */
            // pc_mutex_lock(&client->notify_mutex);
            // QUEUE_FOREACH(q, &client->notify_queue) {
            //     notify = (pc_notify_t* )QUEUE_DATA(q, pc_common_req_t, queue);
            //     if (notify->base.seq_num == seq_num) {

            //         StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_sent - fire sent event, seq_num: " + seqNum);

            //         target = notify;
            //         QUEUE_REMOVE(q);
            //         QUEUE_INIT(q);
            //         break;
            //     }
            // }
            foreach (PcNotify notify in client.NotificationQueue) {
                if (notify.Base.SeqNum == seqNum) {
                    StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_sent - fire sent event, seq_num: " + seqNum);

                    target = notify;
                    client.NotificationQueue.Dequeue();
                    break;
                }
            }
            // pc_mutex_unlock(&client->notify_mutex);

            if (target != null) {
                // Do not call the callback if there is no error.
                if (target.Callback != null && error != null) {
                    target.Callback(target, error);
                }

                // pc_buf_free(&target->base.msg_buf);

                // pc_lib_free((char*)target->base.route);
                target.Base.Route = null;

                if (IsPreAlloc(target.Base.Type)) {

                    // pc_mutex_lock(&client->notify_mutex);
                    notifyType = target.Base.Type;
                    SetPreAllocIdle(ref notifyType);
                    target.Base.Type = notifyType;
                    // pc_mutex_unlock(&client->notify_mutex);

                } else {
                    // pc_lib_free(target);
                }

            } else {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__trans_sent - no pending notify found when transport has sent it, seq num: " + seqNum);
            }
        }

        public static void PcTransQueueEvent(PcClient client, int evType, string arg1, string arg2)
        {
            PcEvent ev;
            int i;

            if (evType >= PitayaNativeConstants.PC_EV_COUNT || evType < 0) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__trans_queue_event - error event type");
                return;
            }

            if (evType == PitayaNativeConstants.PC_EV_USER_DEFINED_PUSH && (arg1 == null || arg2 == null)) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__trans_queue_event - push msg but without a route or msg");
                return;
            }

            if (evType == PitayaNativeConstants.PC_EV_CONNECT_ERROR || evType == PitayaNativeConstants.PC_EV_UNEXPECTED_DISCONNECT
                || evType == PitayaNativeConstants.PC_EV_PROTO_ERROR || evType == PitayaNativeConstants.PC_EV_CONNECT_FAILED || evType == PitayaNativeConstants.PC_EV_RECONNECT_FAILED) {
                if (arg1 == null) {
                    StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__trans_queue_event - event should be with a reason description");
                    return ;
                }
            }

            // StaticPitayaBindingCS.Assert(client.Config.EnablePolling);

            StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_queue_event - add pending event: " + PcClientEvStr(evType));
            // pc_mutex_lock(&client->event_mutex);

            ev = null;
            uint eventType = ev.Type;
            
            for (i = 0; i < PitayaNativeConstants.PC_PRE_ALLOC_EVENT_SLOT_COUNT; i++) {
                if (IsIdle(client.PendingEvents[i].Type)) {
                    ev = client.PendingEvents[i];
                    eventType = ev.Type;
                    SetBusy(ref eventType);
                    ev.Type = eventType;
                    break;
                }
            }

            if (ev == null) {
                // ev = (pc_event_t* )pc_lib_malloc(sizeof(pc_event_t));
                // memset(ev, 0, sizeof(pc_event_t));

                ev.Type = PitayaNativeConstants.PC_DYN_ALLOC;
                eventType = PitayaNativeConstants.PC_DYN_ALLOC;
            }

            SetNetEvent(ref eventType);
            ev.Type = eventType;

            ev.Queue = new Queue<dynamic>();
            ev.Queue.Enqueue(client.PendingEventQueue);

            if (ev.Data is EventEventData eventEventData) {
                eventEventData.EvType = evType;

                if (arg1 != null) {
                    eventEventData.Arg1 = StaticPitayaBindingCS.PcLibStrdup(arg1);
                } else {
                    eventEventData.Arg1 = null;
                }

                if (arg2 != null) {
                    eventEventData.Arg2 = StaticPitayaBindingCS.PcLibStrdup(arg2);
                } else {
                    eventEventData.Arg2 = null;
                }
            }

            // pc_mutex_unlock(&client->event_mutex);
        }

        public static void PcTransFireEvent(PcClient client, int evType, string arg1, string arg2)
        {
            if (client == null) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc_client_fire_event - client is null");
                return ;
            }

            if (client.Config.EnablePolling)
                // pc__trans_queue_event(client, evType, arg1, arg2);
                PcTransQueueEvent(client, evType, arg1, arg2);
            else
                // pc__trans_fire_event(client, evType, arg1, arg2);
                PcTransFireEvent2(client, evType, arg1, arg2);
        }

        public static void PcTransFireEvent2(PcClient client, int evType, string arg1, string arg2)
        {
            Queue<dynamic> q = new Queue<dynamic>();

            if (evType >= PitayaNativeConstants.PC_EV_COUNT || evType < 0) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__transport_fire_event - error event type");
                return;
            }

            if (evType == PitayaNativeConstants.PC_EV_USER_DEFINED_PUSH && (arg1 != null || arg2 != null)) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__transport_fire_event - push msg but without a route or msg");
                return;
            }

            if (evType == PitayaNativeConstants.PC_EV_CONNECT_ERROR || evType == PitayaNativeConstants.PC_EV_UNEXPECTED_DISCONNECT
                || evType == PitayaNativeConstants.PC_EV_PROTO_ERROR || evType == PitayaNativeConstants.PC_EV_CONNECT_FAILED || evType == PitayaNativeConstants.PC_EV_RECONNECT_FAILED) {
                if (arg1 != null) {
                    StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__transport_fire_event - event should be with a reason description");
                    return ;
                }
            }

            StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_fire_event - fire event: " + StaticPcTrans.PcClientEvStr(evType) + ", arg1: " + arg1 != null ? arg1 : "" + ", arg2: " + arg2 != null ? arg2 : "");
            // pc_mutex_lock(&client.State_mutex); 
            switch(evType) {
                case PitayaNativeConstants.PC_EV_CONNECTED:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_CONNECTING);
                    client.State = PitayaNativeConstants.PC_ST_CONNECTED;
                    break;

                case PitayaNativeConstants.PC_EV_CONNECT_ERROR:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_CONNECTING || client.State == PitayaNativeConstants.PC_ST_DISCONNECTING
                            // || client.State == PitayaNativeConstants.PC_ST_CONNECTED);
                    client.State = PitayaNativeConstants.PC_ST_INITED;
                    break;

                case PitayaNativeConstants.PC_EV_CONNECT_FAILED:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_CONNECTING || client.State == PitayaNativeConstants.PC_ST_DISCONNECTING);
                    client.State = PitayaNativeConstants.PC_ST_INITED;
                    break;
                    
                case PitayaNativeConstants.PC_EV_RECONNECT_FAILED:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_CONNECTING || client.State == PitayaNativeConstants.PC_ST_DISCONNECTING);
                    client.State = PitayaNativeConstants.PC_ST_INITED;
                    break;
                    
                case PitayaNativeConstants.PC_EV_DISCONNECT:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_DISCONNECTING || client.State == PitayaNativeConstants.PC_ST_CONNECTED);
                    client.State = PitayaNativeConstants.PC_ST_INITED;
                    break;

                case PitayaNativeConstants.PC_EV_KICKED_BY_SERVER:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_CONNECTED || client.State == PitayaNativeConstants.PC_ST_DISCONNECTING);
                    client.State = PitayaNativeConstants.PC_ST_INITED;
                    break;

                case PitayaNativeConstants.PC_EV_RECONNECT_STARTED:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_CONNECTING || client.State == PitayaNativeConstants.PC_ST_INITED);
                    client.State = PitayaNativeConstants.PC_ST_CONNECTING;
                    break;

                case PitayaNativeConstants.PC_EV_UNEXPECTED_DISCONNECT:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_CONNECTED || client.State == PitayaNativeConstants.PC_ST_DISCONNECTING);
                    client.State = PitayaNativeConstants.PC_ST_INITED;
                    break;
                case PitayaNativeConstants.PC_EV_PROTO_ERROR:
                    // StaticPitayaBindingCS.Assert(client.State == PitayaNativeConstants.PC_ST_CONNECTING || client.State == PitayaNativeConstants.PC_ST_CONNECTED
                            // || client.State == PitayaNativeConstants.PC_ST_DISCONNECTING);
                    client.State = PitayaNativeConstants.PC_ST_CONNECTING;
                    break;
                case PitayaNativeConstants.PC_EV_USER_DEFINED_PUSH:
                    break;
                default:
                    StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__trans_fire_event - unknown network event: " + evType);
                    break;
            }
            // pc_mutex_unlock(&client->state_mutex);

            /* invoke handler */
            // pc_mutex_lock(&client->handler_mutex);
            // QUEUE_FOREACH(q, &client->ev_handlers) {
            //     handler = QUEUE_DATA(q, pc_ev_handler_t, queue);
                // StaticPitayaBindingCS.Assert(handler && handler->cb);
            //     handler->cb(client, ev_type, handler->ex_data, arg1, arg2);
            // }

            foreach (PcEventHandler handler in client.EventHandlers) {
                // StaticPitayaBindingCS.Assert(handler != null && handler.Callback != null);
                handler.Callback(client, evType, handler.ExData, arg1, arg2);
            }

            // pc_mutex_unlock(&client->handler_mutex);
        }

        public static void PcTransPush(PcClient client, string route, PcBuffer buf)
        {
            // pc_assert(client);
            if (client == null || buf.Base == null || buf.Length < 0) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__trans_push - error parameters");
                return;
            }

            if (buf.Length == 0) {
                StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_ERROR, "pc__trans_push - empty buffer");
                return;
            }

            StaticPitayaBindingCS.PcLibLog(PitayaNativeConstants.PC_LOG_INFO, "pc__trans_push - route: " + route);

            /* invoke handler */
            if (client.PushHandler != null) {
                client.PushHandler(client, route, buf);
            }
        }
    }
}