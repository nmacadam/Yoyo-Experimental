using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Yoyo
{
    /// <summary>
    /// Represents a server
    /// </summary>
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
