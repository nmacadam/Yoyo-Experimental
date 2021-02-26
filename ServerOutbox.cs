using System;
using System.Collections.Generic;
using System.Net;

namespace Yoyo
{
    public interface IInbox
    {
        void Respond(uint type, Packet packet);
    }

    public interface IClientInbox : IInbox
    {
        void Respond(ServerPacketType type, Packet packet);
        void OnWelcome(Packet packet);
    }

    public interface IServerInbox : IInbox
    {
        void Respond(ClientPacketType type, Packet packet);
        void OnReceivedWelcome(Packet packet);
    }

    public class ClientMessenger : IClientInbox
    {
        private IClientInfo _info;
        private TcpMessenger _tcpClient;

        private Dictionary<ServerPacketType, Action<Packet>> _responses;

        public TcpMessenger Connection => _tcpClient;

        public ClientMessenger(IClientInfo info)
        {
            _info = info;
            _tcpClient = new TcpMessenger(this, info.Id);

            //Console.WriteLine($"client | creating messenger: id={info.Id}");

            _responses = new Dictionary<ServerPacketType, Action<Packet>>()
            {
                { ServerPacketType.Data,    delegate {} },
                { ServerPacketType.Hello,   OnWelcome   },
            };
        }

        public void Connect(IPAddress address, int port)
        {
            Console.WriteLine($"client | connecting messenger to ip {address.ToString()}:{port}");
            Connection.ConnectTo(address, port);
        }

        public void Respond(uint type, Packet packet)
        {
            // ! oops; the client it getting its own packet :(
            if (!_responses.ContainsKey((ServerPacketType)type))
            {
                Console.WriteLine("?---- " + (ClientPacketType)type);

                return;
            }

            Respond((ServerPacketType)type, packet);
        }

        public void Respond(ServerPacketType type, Packet packet)
        {
            _responses[type].Invoke(packet);
        }

        public void OnWelcome(Packet packet)
        {
            string message = packet.ReadString();
            int id = packet.ReadInt();

            Console.WriteLine($"client | recieved hello: { message }");

            // set client id
            _info.Id = id;

            // send hello ack
            SendWelcomeReceived("Hello server! I am client #" + _info.Id);
        }

        private void Send(Packet packet)
        {
            packet.WriteLength();
            _tcpClient.Send(packet);
        }

        public void SendWelcomeReceived(string message)
        {
            Console.WriteLine("client | sending server hello ack packet...");

            using (Packet packet = new Packet((uint)ClientPacketType.HelloReceived))
            {
                packet.Write(message);
                Send(packet);
            }
        }
    }


    //public class ClientInbox : IInbox
    //{
    //    private IClientInfo _client;
    //    private ClientOutbox _outbox;
    //    private Dictionary<ServerPacketType, Action<Packet>> _responses;

    //    public ClientInbox(IClientInfo client, ClientOutbox outbox)
    //    {
    //        _client = client;
    //        _outbox = outbox;
    //        _responses = new Dictionary<ServerPacketType, Action<Packet>>()
    //        {
    //            { ServerPacketType.Data,    delegate {} },
    //            { ServerPacketType.Hello,   OnWelcome   },
    //        };
    //    }

    //    public void Respond(uint type, Packet packet)
    //    {
    //        Respond((ServerPacketType)type, packet);
    //    }

    //    public void Respond(ServerPacketType type, Packet packet)
    //    {
    //        _responses[type].Invoke(packet);
    //    }

    //    public void OnWelcome(Packet packet)
    //    {
    //        string message = packet.ReadString();
    //        int id = packet.ReadInt();

    //        Console.WriteLine($"client | recieved hello: { message }");

    //        // set client id
    //        _client.Id = id;

    //        // send hello ack
    //        _outbox.SendWelcomeReceived("Hello server! I am client #" + _client.Id);
    //    }
    //}

    //public class ClientOutbox
    //{
    //    private TcpMessenger _client;

    //    public ClientOutbox(TcpMessenger client)
    //    {
    //        _client = client;
    //    }

    //    private void Send(Packet packet)
    //    {
    //        packet.WriteLength();
    //        _client.Send(packet);
    //        //_server.GetClientInfo(toClient).Messenger.Send(packet);
    //    }

    //    public void SendWelcomeReceived(string message)
    //    {
    //        Console.WriteLine("client | sending server hello ack packet...");

    //        using (Packet packet = new Packet((uint)ClientPacketType.HelloReceived))
    //        {
    //            packet.Write(message);
    //            //packet.Write(toClient);

    //            Send(packet);
    //        }
    //    }
    //}

    public class ServerOutbox
    {
        private Server _server;

        public ServerOutbox(Server server)
        {
            _server = server;
        }

        private void Send(int toClient, Packet packet)
        {
            packet.WriteLength();
            _server.GetClientInfo(toClient).Messenger.Connection.Send(packet);
        }

        private void SendToAll(Packet packet)
        {
            packet.WriteLength();
            for (int i = 0; i < _server.MaxPlayers; i++)
            {
                _server.GetClientInfo(i).Messenger.Connection.Send(packet);
            }
        }

        private void SendToAll(int exclude, Packet packet)
        {
            packet.WriteLength();
            for (int i = 0; i < _server.MaxPlayers; i++)
            {
                if (i == exclude) continue;
                _server.GetClientInfo(i).Messenger.Connection.Send(packet);
            }
        }

        public void SendWelcome(int toClient, string message)
        {
            Console.WriteLine("server | sending client hello packet...");

            using (Packet packet = new Packet((uint)ServerPacketType.Hello))
            {
                packet.Write(message);
                packet.Write(toClient);

                Send(toClient, packet);
            }
        }
    }
}
