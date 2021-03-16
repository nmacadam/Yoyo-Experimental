using System;
using System.Collections.Generic;

namespace Yoyo
{
    /// <summary>
    /// Sends the client's preconstructed messages and responds to incoming packet content
    /// </summary>
    class ClientMailbox
    {
        private NetworkReceiver _client;
        private Dictionary<ServerPacketType, Action<Packet>> _responses;

        public ClientMailbox(NetworkReceiver client)
        {
            _client = client;

            _responses = new Dictionary<ServerPacketType, Action<Packet>>()
            {
                { ServerPacketType.Hello, OnWelcomeReceived },
                { ServerPacketType.Heartbeat, OnHeartbeatReceived }
            };
        }

        public void Respond(uint type, Packet packet)
        {
            Respond((ServerPacketType)type, packet);
        }

        public void Respond(ServerPacketType type, Packet packet)
        {
            if (!_responses.ContainsKey(type))
            {
                Console.WriteLine($"client | ERROR: No response for packet type '{type}'");
                return;
            }

            _responses[type].Invoke(packet);
        }

        public void OnWelcomeReceived(Packet received)
        {
            Packet packet = new Packet(_client.Info.Sequence, (uint)ClientPacketType.Ack);
            packet.Write((uint)ServerPacketType.Hello);

            _client.Send(packet);
        }

        public void OnHeartbeatReceived(Packet received)
        {
            Packet packet = new Packet(_client.Info.Sequence, (uint)ClientPacketType.Ack);
            packet.Write((uint)ServerPacketType.Heartbeat);

            _client.Send(packet);
        }
    }
}
