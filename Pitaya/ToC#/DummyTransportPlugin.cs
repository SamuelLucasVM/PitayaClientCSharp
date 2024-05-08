using System;
using System.Runtime.CompilerServices;

namespace Pitaya.NativeImpl
{
    public static class StaticDummyTransport {
        public static int DummyInit(PcTransport trans, PcClient client)
        {
            DummyTransport dTr = new DummyTransport{Base=trans};
            // StaticPitayaBindingCS.Assert(dTr);

            dTr.Client = client;

            return PitayaNativeConstants.PC_RC_OK;
        }

        public static int DummyConnect(PcTransport trans, string host, int port, string handshakeOpt)
        {
            DummyTransport dTr = new DummyTransport{Base=trans};
            // StaticPitayaBindingCS.Assert(dTr);

            StaticPcTrans.PcTransFireEvent(dTr.Client, PitayaNativeConstants.PC_EV_CONNECTED, null, null);

            return PitayaNativeConstants.PC_RC_OK;
        }

        public static int DummySend(PcTransport trans, string route, uint seqNum, PcBuffer msgBuff, uint reqId, int timeout)
        {
            DummyTransport dTr = new DummyTransport{Base=trans};
            // StaticPitayaBindingCS.Assert(dTr);

            if (reqId == PitayaNativeConstants.PC_NOTIFY_PUSH_REQ_ID) {
                StaticPcTrans.PcTransSent(dTr.Client, seqNum, null);
            } else {
                PcBuffer dummyResp = new PcBuffer();
                dummyResp.Base = System.Text.Encoding.UTF8.GetBytes(StaticPitayaBindingCS.PcLibStrdup("dummy resp"));
                dummyResp.Length = 10;

                StaticPcTrans.PcTransResp(dTr.Client, reqId, dummyResp, null);

                // pc_buf_free(&dummy_resp);
            }

            return PitayaNativeConstants.PC_RC_OK;
        }

        public static int DummyDisconnect(PcTransport trans)
        {
            DummyTransport dTr = new DummyTransport{Base=trans};
            // StaticPitayaBindingCS.Assert(dTr);

            StaticPcTrans.PcTransFireEvent(dTr.Client, PitayaNativeConstants.PC_EV_DISCONNECT, null, null);

            return PitayaNativeConstants.PC_RC_OK;
        }

        public static int DummyCleanup(PcTransport trans)
        {
            return PitayaNativeConstants.PC_RC_OK;
        }

        public static IntPtr DummyInternalData(PcTransport trans)
        {
            return IntPtr.Zero;
        }

        public static PcTransportPlugin DummyPlugin(PcTransport trans)
        {
            return PcTrDummyTransPlugin();
        }

        public static int DummyConnQuality(PcTransport trans)
        {
            return 0;
        }

        public static PcTransport DummyTransCreate(PcTransportPlugin plugin)
        {
            PcTransport trans = new PcTransport {
                Init = DummyInit,
                Connect = DummyConnect,
                Send = DummySend,
                Disconnect = DummyDisconnect,
                Cleanup = DummyCleanup,
                InternalData = DummyInternalData,
                Plugin = DummyPlugin,
                Quality = DummyConnQuality
            };

            return trans;
        }

        public static void DummyTransRelease(PcTransportPlugin plugin, PcTransport trans)
        {
            // pc_lib_free(trans);
        }

        public static PcTransportPlugin PcTrDummyTransPlugin()
        {
            TransportCreateDelegate createDelegate = DummyTransCreate;
            TransportReleaseDelegate releaseDelegate = DummyTransRelease;
            int transportName = PitayaNativeConstants.PC_TR_NAME_DUMMY;

            PcTransportPlugin instance = new PcTransportPlugin (
                createDelegate,
                releaseDelegate,
                transportName
            );
            return instance;
        }
    }
}