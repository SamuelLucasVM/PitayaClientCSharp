using System;
using System.Net;
using System.Net.Sockets;
using Pitaya.NativeImpl;

namespace Pitaya
{
    class Program {
        public static PitayaClient client;

        public static void Main(String[] args) {
            client = new PitayaClient();

            client.Connect("localhost", 3250);

            const string route = "requestor.requestor.getaccounts";

            // client.SendRequest(route, []);

            // while(true) {}
        }
    }
}