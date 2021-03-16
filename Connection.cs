using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Yoyo
{
    public class ProtocolInfo
    {
        private uint _sequenceNumber = 0;

        public uint Sequence => _sequenceNumber++;
    }

    public class SocketState
    {
        public Socket WorkSocket = null;
        public ProtocolInfo Info;
        public const int BufferSize = 4096;
        public byte[] ReceiveBuffer = new byte[BufferSize];
    }

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

    class ConnectionInfo
    {
        private Socket _socket;
        private ServerMailbox _mailbox;

        public ServerMailbox Mailbox => _mailbox;
        public Socket Socket { get => _socket; set => _socket = value; }

        public ConnectionInfo(Socket socket, int connection, NetworkListener listener)
        {
            _socket = socket;
            _mailbox = new ServerMailbox(connection, listener);
        }
    }

    //class ReceiverInfo
    //{
    //    ServerMailbox Mailbox = new ServerMailbox;
    //}

    class NetworkReceiver
    {
        public class ReceiverSocketState : SocketState
        {
            public NetworkReceiver Receiver;
            public ClientMailbox Mailbox;
        }

        private IPAddress _ip;
        private int _port;

        private Socket _socket;

        private ClientMailbox _mailbox;

        private ProtocolInfo _info = new ProtocolInfo();
        public ProtocolInfo Info => _info;

        public NetworkReceiver(IPAddress ip, int port)
        {
            _ip = ip;
            _port = port;

            _mailbox = new ClientMailbox(this);
        }

        public void Connect()
        {
            IPEndPoint remoteEP = new IPEndPoint(_ip, _port);

            // Create a TCP/IP socket.  
            _socket = new Socket(_ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            ReceiverSocketState state = new ReceiverSocketState();
            state.WorkSocket = _socket;
            state.Info = _info;
            state.Receiver = this;
            state.Mailbox = _mailbox;

            _socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), state);
        }

        public void Send(Packet packet)
        {
            ReceiverSocketState state = new ReceiverSocketState();
            state.WorkSocket = _socket;
            state.Info = _info;
            state.Receiver = this;
            state.Mailbox = _mailbox;

            packet.WriteLength();

            state.WorkSocket.BeginSend(packet.ToArray(), 0, packet.Length(), 0, SendCallback, state);
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                ReceiverSocketState state = (ReceiverSocketState)ar.AsyncState;

                // Complete the connection.  
                state.WorkSocket.EndConnect(ar);

                if (!state.WorkSocket.Connected)
                {
                    Console.WriteLine("Failed to connect");
                    // failed to connect

                    return;
                }

                Console.WriteLine($"client | connected to {state.WorkSocket.RemoteEndPoint}");

                state.WorkSocket.BeginReceive(state.ReceiveBuffer, 0, SocketState.BufferSize, SocketFlags.None, ReceiveCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                ReceiverSocketState state = (ReceiverSocketState)ar.AsyncState;

                // get the size of the recieved data
                int receivedSize = state.WorkSocket.EndReceive(ar);

                if (receivedSize <= 0)
                {
                    // todo: disconnect
                    return;
                }

                // copy the recieved data out of the recieve buffer and into a seperate buffer we'll operate on
                byte[] buffer = new byte[receivedSize];
                Array.Copy(state.ReceiveBuffer, buffer, receivedSize);

                Console.WriteLine("client | received packet...");

                // for now, lets assume its a single, entire packet, that just contains a string
                Packet received = new Packet(buffer);
                int packetLength = received.ReadLength();
                PacketHeader header = received.ReadHeader();
                string message = received.ReadString();

                Console.WriteLine($"client | packet ({(ServerPacketType)header.PacketType}): {message}");

                // respond
                state.Mailbox.Respond((ServerPacketType)header.PacketType, received);

                //Packet packet = new Packet(state.Info.Sequence, (uint)ClientPacketType.HelloReceived);
                //packet.Write("Hello server!");

                //packet.WriteLength();

                //state.WorkSocket.BeginSend(packet.ToArray(), 0, packet.Length(), 0, SendCallback, state);



                // todo: clear out processed data from our main packet

                // begin receiving again
                state.WorkSocket.BeginReceive(state.ReceiveBuffer, 0, SocketState.BufferSize, SocketFlags.None, ReceiveCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object.  
                ReceiverSocketState state = (ReceiverSocketState)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = state.WorkSocket.EndSend(ar);
                Console.WriteLine("client | sent {0} bytes to server.", bytesSent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }

    class NetworkListener
    {
        public class ListenerSocketState : SocketState
        {
            public NetworkListener Listener;
            public int MaxConnections;
            public Dictionary<int, ConnectionInfo> Connections;
            public int ConnectionId = -1;
        }

        private IPAddress _ip;
        private int _port;

        private ProtocolInfo _info = new ProtocolInfo();

        private int _maxConnections;
        private Dictionary<int, ConnectionInfo> _connections = new Dictionary<int, ConnectionInfo>();

        public ProtocolInfo Info => _info;

        public NetworkListener(IPAddress ip, int port, int maxConnections)
        {
            _ip = ip;
            _port = port;

            _maxConnections = maxConnections;
            for (int i = 0; i < _maxConnections; i++)
            {
                _connections.Add(i, null);
            }
        }

        public void Listen()
        {
            IPEndPoint localEndPoint = new IPEndPoint(_ip, _port);
            Socket listener = new Socket(_ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                ListenerSocketState state = new ListenerSocketState();
                state.Listener = this;
                state.WorkSocket = listener;
                state.Info = _info;
                state.MaxConnections = _maxConnections;
                state.Connections = _connections;

                listener.BeginAccept(AcceptCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Send(int connection, Packet packet)
        {
            ListenerSocketState state = new ListenerSocketState();
            state.WorkSocket = _connections[connection].Socket;

            packet.WriteLength();

            state.WorkSocket.BeginSend(packet.ToArray(), 0, packet.Length(), 0, SendCallback, state);
        }

        public void SendAll(Packet packet)
        {
            for (int i = 0; i < _maxConnections; i++)
            {
                if (_connections[i] != null)
                {
                    Send(i, packet);
                }
            }
        }

        public void SendAll(int excludeClient, Packet packet)
        {
            for (int i = 0; i < _maxConnections; i++)
            {
                if (i != excludeClient && _connections[i] != null)
                {
                    Send(i, packet);
                }
            }
        }

        // static callbacks

        /// <summary>
        /// Called when a listener accepts a client
        /// </summary>
        private static void AcceptCallback(IAsyncResult ar)
        {
            // Get the socket that handles the client request.
            ListenerSocketState state = (ListenerSocketState)ar.AsyncState;
            Socket listener = state.WorkSocket;
            Socket handler = listener.EndAccept(ar);

            // Update the work socket to be the client handler
            state.WorkSocket = handler;

            // Try to find an open slot
            bool didConnect = false;
            for (int i = 0; i < state.MaxConnections; i++)
            {
                if (state.Connections[i] == null)
                {
                    //state.Connections[i].Socket = state.WorkSocket;
                    state.Connections[i] = new ConnectionInfo(state.WorkSocket, i, state.Listener);
                    state.ConnectionId = i;
                    didConnect = true;
                }
            }

            // handle a failed connection
            if (!didConnect)
            {
                // Send a server full message
                Packet failurePacket = new Packet(state.Info.Sequence, (uint)ServerPacketType.Full);
                failurePacket.Write("Server is full! Sorry!");
                failurePacket.WriteLength();
                state.WorkSocket.BeginSend(failurePacket.ToArray(), 0, failurePacket.Length(), 0, SendCallback, state);

                return;
            }

            // Send a hello message
            state.Connections[state.ConnectionId].Mailbox.SendWelcome();

            // Listen for a response
            state.WorkSocket.BeginReceive(state.ReceiveBuffer, 0, SocketState.BufferSize, SocketFlags.None, ReceiveCallback, state);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object.  
                ListenerSocketState state = (ListenerSocketState)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = state.WorkSocket.EndSend(ar);
                Console.WriteLine("server | sent {0} bytes to client.", bytesSent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                ListenerSocketState state = (ListenerSocketState)ar.AsyncState;

                // get the size of the recieved data
                int receivedSize = state.WorkSocket.EndReceive(ar);

                // copy the recieved data out of the recieve buffer and into a seperate buffer we'll operate on
                byte[] buffer = new byte[receivedSize];
                Array.Copy(state.ReceiveBuffer, buffer, receivedSize);

                Console.WriteLine("server | received packet...");
                
                Packet packet = new Packet(buffer);
                int packetLength = packet.ReadLength();
                PacketHeader header = packet.ReadHeader();
                //string message = packet.ReadString();

                //Console.WriteLine($"server | packet ({(ClientPacketType)header.PacketType}): {message}");

                // respond
                state.Connections[state.ConnectionId].Mailbox.Respond((ClientPacketType)header.PacketType, packet);

                // start heartbeat loop
                state.Connections[state.ConnectionId].Mailbox.SendHeartbeat();

                // todo: clear out processed data from our main packet

                // begin receiving again
                state.WorkSocket.BeginReceive(state.ReceiveBuffer, 0, SocketState.BufferSize, SocketFlags.None, ReceiveCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
