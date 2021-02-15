using System;
using System.Collections.Generic;

namespace Yoyo
{
    public interface IInbox
    {
        void Respond(uint type, Packet packet);
    }

    public class ClientInbox : IInbox
    {
        private Client _client;
        private Dictionary<ServerPacketType, Action<Packet>> _responses;

        public ClientInbox(Client client)
        {
            _client = client;
            _responses = new Dictionary<ServerPacketType, Action<Packet>>()
            {
                { ServerPacketType.Data,    delegate {} },
                { ServerPacketType.Hello,   OnWelcome   },
            };
        }

        public void Respond(uint type, Packet packet)
        {
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


            // send hello ack
        }
    }

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
            _server.GetClientInfo(toClient).Messenger.Send(packet);
        }

        private void SendToAll(Packet packet)
        {
            packet.WriteLength();
            for (int i = 0; i < _server.MaxPlayers; i++)
            {
                _server.GetClientInfo(i).Messenger.Send(packet);
            }
        }

        private void SendToAll(int exclude, Packet packet)
        {
            packet.WriteLength();
            for (int i = 0; i < _server.MaxPlayers; i++)
            {
                if (i == exclude) continue;
                _server.GetClientInfo(i).Messenger.Send(packet);
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
