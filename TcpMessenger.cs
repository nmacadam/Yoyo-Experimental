using System;
using System.Net;
using System.Net.Sockets;

namespace Yoyo
{
    public class TcpMessenger
    {
        private IInbox _inbox;
        private TcpClient _socket;

        private NetworkStream _networkStream;

        private const int _bufferSize = 4096;
        private byte[] _receiveBuffer;
        private Packet _receivedPacket;

        private int _id = -1;
        private uint _sequenceNumber = 0;

        public bool IsActive => _socket != null;

        public TcpMessenger(IInbox inbox)
        {
            _inbox = inbox;
            _receiveBuffer = new byte[_bufferSize];
        }

        public TcpMessenger(IInbox inbox, int id)
        {
            _inbox = inbox;
            _id = id;
            _receiveBuffer = new byte[_bufferSize];
        }

        ///// <summary>
        ///// Creates a TcpMessenger actively reading from the connected TcpClient
        ///// </summary>
        //public static TcpMessenger GetReader(TcpClient client)
        //{
        //    TcpMessenger messenger = new TcpMessenger();

        //    client.ReceiveBufferSize = Packet.PacketSize;
        //    client.SendBufferSize = Packet.PacketSize;

        //    messenger._socket = client;
        //    messenger._networkStream = messenger._socket.GetStream();

        //    messenger._networkStream.BeginRead(messenger._receiveBuffer, 0, Packet.PacketSize, messenger.ReceiveCallback, null);

        //    // send hello packet

        //    return messenger;
        //}

        // server side
        public void ReadFrom(Server server, TcpClient client)
        {
            _socket = client;
            _socket.ReceiveBufferSize = _bufferSize;
            _socket.SendBufferSize = _bufferSize;
            _networkStream = _socket.GetStream();

            _receivedPacket = new Packet();

            // begin reading from stream
            _networkStream.BeginRead(_receiveBuffer, 0, _bufferSize, ReceiveCallback, null);

            // send hello packet
            server.Outbox.SendWelcome(_id, "Welcome to the server!");
        }

        //public void ReadFrom(Server server, IPAddress address, int port)
        //{
        //    // CHANGE
        //    _socket = new TcpClient(new IPEndPoint(address, port));
        //    _socket.ReceiveBufferSize = _bufferSize;
        //    _socket.SendBufferSize = _bufferSize;
        //    _networkStream = _socket.GetStream();

        //    // begin reading from stream
        //    _networkStream.BeginRead(_receiveBuffer, 0, _bufferSize, ReceiveCallback, null);

        //    // send hello packet
        //    ServerOutbox.Welcome(server, _id, "Welcome to the server!");
        //}

        // maybe convert into a factory constructor
        // client side
        public void ConnectTo(IPAddress address, int port)
        {
            // CHANGE
            _socket = new TcpClient();
            _socket.ReceiveBufferSize = _bufferSize;
            _socket.SendBufferSize = _bufferSize;

            if (_socket.Connected)
            {
                Console.WriteLine("Socket is already connected");
            }

            _socket.BeginConnect(address, port, ConnectCallback, null);
        }

        public void Send(Packet packet)
        {
            try
            {
                if (_socket == null) return;
                _networkStream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            //try
            {
                int receivedSize = _networkStream.EndRead(ar);
                if (receivedSize <= 0)
                {
                    // disconnect
                    return;
                }

                byte[] buffer = new byte[receivedSize];
                Array.Copy(_receiveBuffer, buffer, receivedSize);

                Console.WriteLine("client | received packet...");

                // handle received data
                // lets just assume its exactly one packet for now
                _receivedPacket.Reset();
                _receivedPacket = new Packet(buffer);

                int length = _receivedPacket.ReadInt();
                Console.WriteLine("client | packet length: " + length);

                int packetId = _receivedPacket.ReadInt();
                Console.WriteLine("client | packet id: " + (ServerPacketType)packetId);

                _inbox.Respond((uint)packetId, _receivedPacket);

                //_receivedPacket.Reset(HandleData(buffer));
                _receivedPacket.Reset();

                // start reading again
                _networkStream.BeginRead(_receiveBuffer, 0, _bufferSize, ReceiveCallback, null);
            }
            //catch (Exception e)
            {
                //Console.WriteLine(e.Message);
                // disconnect
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            _socket.EndConnect(ar);

            if (!_socket.Connected)
            {
                Console.WriteLine("Failed to connect");
                // failed to connect
                return;
            }

            Console.WriteLine($"client | connected to {_socket.Client.RemoteEndPoint}");

            _networkStream = _socket.GetStream();
            _receivedPacket = new Packet();

            _networkStream.BeginRead(_receiveBuffer, 0, _bufferSize, ReceiveCallback, null);
        }

        private bool HandleData(byte[] data)
        {
            // might need to not be local
            int packetLength = 0;
            _receivedPacket.SetBytes(data);

            if (_receivedPacket.UnreadLength() >= sizeof(int))
            {
                packetLength = _receivedPacket.ReadInt();
                if (packetLength <= 0)
                {
                    return true;
                }
            }

            while (packetLength > 0 && packetLength <= _receivedPacket.UnreadLength())
            {
                byte[] packetBytes = _receivedPacket.ReadBytes(packetLength);

                // use thread manager
                using (Packet packet = new Packet(packetBytes))
                {
                    int packetId = _receivedPacket.ReadInt();
                    //inbox.Respond((uint)packetId, packet);
                }

                packetLength = 0;
                if (_receivedPacket.UnreadLength() >= sizeof(int))
                {
                    packetLength = _receivedPacket.ReadInt();
                    if (packetLength <= 0)
                    {
                        return true;
                    }
                }
            }

            if (packetLength <= 1)
            {
                return true;
            }

            return false;
        }
    }
}
