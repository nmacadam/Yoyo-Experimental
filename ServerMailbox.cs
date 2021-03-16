using System;
using System.Collections.Generic;

namespace Yoyo
{
    /// <summary>
    /// Sends the server's preconstructed messages and responds to incoming packet content
    /// </summary>
    class ServerMailbox
    {
        private int _connection;
        private NetworkListener _listener;
        private Dictionary<ClientPacketType, Action<Packet>> _responses;

        public ServerMailbox(int connection, NetworkListener listener)
        {
            _listener = listener;
            _connection = connection;

            _responses = new Dictionary<ClientPacketType, Action<Packet>>()
            {
                { ClientPacketType.Ack, OnAckReceived },
            };
        }

        public void Respond(uint type, Packet packet)
        {
            Respond((ClientPacketType)type, packet);
        }

        public void Respond(ClientPacketType type, Packet packet)
        {
            if (!_responses.ContainsKey(type))
            {
                Console.WriteLine($"server | ERROR: No response for packet type '{type}'");
                return;
            }

            _responses[type].Invoke(packet);
        }

        public void SendWelcome()
        {
            // might need to move protocol info here
            Packet packet = new Packet(_listener.Info.Sequence, (uint)ServerPacketType.Hello);
            packet.Write("Hello client!");

            _listener.Send(_connection, packet);
        }

        public void SendHeartbeat()
        {
            // might need to move protocol info here
            Packet packet = new Packet(_listener.Info.Sequence, (uint)ServerPacketType.Heartbeat);
            packet.Write("OK?");

            _listener.Send(_connection, packet);
        }

        public void OnAckReceived(Packet recieved)
        {
            Console.WriteLine($"server | recieved ack");
        }
    }
}
