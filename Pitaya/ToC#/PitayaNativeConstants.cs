namespace Pitaya.NativeImpl
{
    public static class PitayaNativeConstants
    {
        public const int PC_EV_USER_DEFINED_PUSH = 0;
        public const int PC_EV_CONNECTED = 1;
        public const int PC_EV_CONNECT_ERROR = 2;
        public const int PC_EV_CONNECT_FAILED = 3;
        public const int PC_EV_DISCONNECT = 4;
        public const int PC_EV_KICKED_BY_SERVER = 5;
        public const int PC_EV_UNEXPECTED_DISCONNECT = 6;
        public const int PC_EV_PROTO_ERROR = 7;
        public const int PC_EV_RECONNECT_FAILED = 8;
        public const int PC_EV_RECONNECT_STARTED = 9;
        public const int PC_EV_COUNT = 10;

        public static readonly uint PC_EV_TYPE_NOTIFY_SENT = 0x10;
        public static readonly uint PC_EV_TYPE_RESP = 0x20;
        public static readonly uint PC_EV_TYPE_NET_EVENT = 0x40;
        public static readonly int PC_EV_TYPE_PUSH = 0x80;
        public static readonly uint PC_EV_TYPE_MASK = 0xf0;

        public static readonly uint PC_PRE_ALLOC = 0x1;
        public static readonly uint PC_DYN_ALLOC = 0x0;
        public static readonly int PC_ALLOC_MASK = 0x1;

        public static readonly uint PC_PRE_ALLOC_ST_IDLE = 0x0;
        public static readonly uint PC_PRE_ALLOC_ST_BUSY = 0x2;
        public static readonly uint PC_PRE_ALLOC_ST_MASK = 0x2;

        public static readonly uint PC_REQ_TYPE_NOTIFY = 0x10;
        public static readonly uint PC_REQ_TYPE_REQUEST = 0x20;
        public static readonly int PC_REQ_TYPE_MASK = 0xf0;

        public static readonly int PC_EV_INVALID_HANDLER_ID = -1;

        /* +2 for net event */
        public static readonly int PC_PRE_ALLOC_EVENT_SLOT_COUNT = PC_PRE_ALLOC_NOTIFY_SLOT_COUNT + PC_PRE_ALLOC_REQUEST_SLOT_COUNT + 2;

        /**
        * some tunable arguments
        */
        public static readonly int PC_TRANSPORT_PLUGIN_SLOT_COUNT = 8;
        public static readonly int PC_PRE_ALLOC_REQUEST_SLOT_COUNT = 4; 
        public static readonly int PC_PRE_ALLOC_NOTIFY_SLOT_COUNT = 4;
        public static readonly int PC_TIMEOUT_CHECK_INTERVAL = 2;
        public static readonly int PC_HEARTBEAT_TIMEOUT_FACTOR = 2;
        public static readonly int PC_TCP_READ_BUFFER_SIZE = (1 << 16);

        /**
        * error code
        */
        public static readonly int PC_RC_OK = 0;
        public static readonly int PC_RC_ERROR = -1;
        public static readonly int PC_RC_TIMEOUT = -2;
        public static readonly int PC_RC_INVALID_JSON = -3;
        public static readonly int PC_RC_INVALID_ARG = -4;
        public static readonly int PC_RC_NO_TRANS = -5;
        public static readonly int PC_RC_INVALID_THREAD = -6;
        public static readonly int PC_RC_TRANS_ERROR = -7;
        public static readonly int PC_RC_INVALID_ROUTE = -8;
        public static readonly int PC_RC_INVALID_STATE = -9;
        public static readonly int PC_RC_NOT_FOUND = -10;
        public static readonly int PC_RC_RESET = -11;
        public static readonly int PC_RC_SERVER_ERROR = -12;
        public static readonly int PC_RC_UV_ERROR = -13;
        public static readonly int PC_RC_NO_SUCH_FILE = -14;
        public static readonly int PC_RC_MIN = -15;

        /**
        * reconnect max retry
        */
        public static readonly int PC_ALWAYS_RETRY = -1;


        /**
        * builtin transport name
        */
        public static readonly int PC_TR_NAME_UV_TCP = 0;
        public static readonly int PC_TR_NAME_UV_TLS = 1;
        public static readonly int PC_TR_NAME_DUMMY = 7;

        /**
        * log level
        */
        public const int PC_LOG_DEBUG = 0;
        public const int PC_LOG_INFO = 1;
        public const int PC_LOG_WARN = 2;
        public const int PC_LOG_ERROR = 3;
        public const int PC_LOG_DISABLE = 4;

        /**
        * client state
        */
        public const int PC_ST_INITED = 0;
        public const int PC_ST_CONNECTING = 1;
        public const int PC_ST_CONNECTED = 2;
        public const int PC_ST_DISCONNECTING = 3;
        public const int PC_ST_UNKNOWN = 4;
        public const int PC_ST_COUNT = 5;

        /**
        * special request id
        */
        public static readonly uint PC_NOTIFY_PUSH_REQ_ID = (uint)0;
        public static readonly uint PC_INVALID_REQ_ID = unchecked((uint)-1);

        public static readonly string[] EvStrings =
        {
            "PC_EV_USER_DEFINED_PUSH",
            "PC_EV_CONNECTED",
            "PC_EV_CONNECT_ERROR",
            "PC_EV_CONNECT_FAILED",
            "PC_EV_DISCONNECT",
            "PC_EV_KICKED_BY_SERVER",
            "PC_EV_UNEXPECTED_DISCONNECT",
            "PC_EV_PROTO_ERROR",
            "PC_EV_RECONNECT_FAILED",
            "PC_EV_RECONNECT_STARTED"
        };

        public static readonly string[] RcStrings = {
            "PC_RC_OK",
            "PC_RC_ERROR",
            "PC_RC_TIMEOUT",
            "PC_RC_INVALID_JSON",
            "PC_RC_INVALID_ARG",
            "PC_RC_NO_TRANS",
            "PC_RC_INVALID_THREAD",
            "PC_RC_TRANS_ERROR",
            "PC_RC_INVALID_ROUTE",
            "PC_RC_INVALID_STATE",
            "PC_RC_NOT_FOUND",
            "PC_RC_RESET",
            "PC_RC_SERVER_ERROR",
            "PC_RC_UV_ERROR",
            "PC_RC_NO_SUCH_FILE",
            ""
        };

        public static readonly string[] StateStrings = {
            "PC_ST_INITED",
            "PC_ST_CONNECTING",
            "PC_ST_CONNECTED",
            "PC_ST_DISCONNECTING",
            "PC_ST_UNKNOWN",
            ""
        };
        
        /**
        * disable timeout
        */
        public static readonly int PC_WITHOUT_TIMEOUT = -1;
    }
}