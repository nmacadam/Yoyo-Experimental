using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yoyo
{
    public class Client : IClientInfo
    {
        private ClientMessenger _messenger;
        //private TcpMessenger _messenger;

        //private ClientInbox _inbox;
        //private ClientOutbox _outbox;

        private IPAddress _address;
        private int _port;

        private int _id = -1;

        //public IInbox Inbox => _inbox;

        public int Id
        {
            get
            {
                return _id;
            }
            set
            {
                Console.WriteLine($"client | recieved id: { value }");
                _id = value;
            }
        }

        public Client(IPAddress address, int port)
        {
            // not gonna work in this order :(
            _messenger = new ClientMessenger(this);

            //_inbox = new ClientInbox(this, _outbox);
            //_messenger = new TcpMessenger(_inbox);
            //_outbox = new ClientOutbox(_messenger);

            _address = address;
            _port = port;
        }

        public void Connect()
        {
            Console.WriteLine($"client | connecting to {_address}:{_port}...");
            //_messenger.Connection.ConnectTo(_address, _port);
            _messenger.Connect(_address, _port);

            // send hello packet
        }
    }

    public class Server
    {
        private TcpListener _server;

        private ServerOutbox _outbox;

        //private bool _isActive = false;
        private int _maxPlayers = 10;
        private Dictionary<int, ClientInfo> _clients;

        public int MaxPlayers => _maxPlayers;
        public ServerOutbox Outbox => _outbox;

        public Server(IPAddress address, int port)
        {
            _outbox = new ServerOutbox(this);

            //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //IPAddress ipAddress = ipHostInfo.AddressList[0];
            //IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            //_server = new TcpListener(localEndPoint);
            _server = new TcpListener(address, port);

           // Console.WriteLine($"server | creating server on {ipHostInfo.HostName} at {localEndPoint}");

            _clients = new Dictionary<int, ClientInfo>();
            for (int i = 1; i <= _maxPlayers; i++)
            {
                _clients.Add(i, new ClientInfo(i));
            }
        }

        public void Listen()
        {
            Console.WriteLine("server | listening for clients...");
            //_isActive = true;
            _server.Start();
            _server.BeginAcceptTcpClient(TcpConnectCallback, _server);
        }

        private void TcpConnectCallback(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener)ar.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(ar);

            Console.WriteLine($"server | incoming connection from {client.Client.RemoteEndPoint}...");
            
            // start listening again
            _server.BeginAcceptTcpClient(new AsyncCallback(TcpConnectCallback), _server);

            // Find an open slot and connect it
            for (int i = 1; i <= _maxPlayers; i++)
            {
                if (!_clients[i].Messenger.Connection.IsActive)
                {
                    Console.WriteLine($"server | client at {client.Client.RemoteEndPoint} successfully connected!");
                    _clients[i].Messenger.Connection.ReadFrom(this, client);
                    return;
                }
            }

            Console.WriteLine("server | client failed to connect! server full!");
        }

        public ClientInfo GetClientInfo(int id)
        {
            return _clients[id];
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
