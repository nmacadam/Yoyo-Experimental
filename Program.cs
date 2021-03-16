using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Yoyo
{
    public class Client
    {
        public class ClientState
        {
            public Socket WorkSocket = null;
            public const int BufferSize = 4096;
            public byte[] ReceiveBuffer = new byte[BufferSize];
        }

        private IPAddress _ip;
        private int _port;

        private NetworkReceiver _networkReceiver;

        public Client(IPAddress ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public void Connect()
        {
            _networkReceiver = new NetworkReceiver(_ip, _port);
            _networkReceiver.Connect();
        }
    }

    public class Server
    {
        private IPAddress _ip;
        private int _port;

        private NetworkListener _networkListener;

        public Server(IPAddress ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public void Listen()
        {
            _networkListener = new NetworkListener(_ip, _port, 10);
            _networkListener.Listen();
        }
    }

    class Program
    {
        public static void ServerThread()
        {
            Server s = new Server(IPAddress.Any, 11000);
            s.Listen();
        }

        public static void ClientThread()
        {
            Client c1 = new Client(IPAddress.Parse("127.0.0.1"), 11000);
            c1.Connect();
        }

        static void Main(string[] args)
        {
            Console.Title = "Yoyo Networking";

            Thread s = new Thread(new ThreadStart(ServerThread));
            Thread c = new Thread(new ThreadStart(ClientThread));

            s.Start();

            Thread.Sleep(300);

            c.Start();

            s.Join();
            c.Join();

            Console.ReadKey();

            return;
        }
    }
}
