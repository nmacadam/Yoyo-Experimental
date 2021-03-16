using System;
using System.Net;
using System.Net.Sockets;

namespace Yoyo
{
    /// <summary>
    /// Represents a client
    /// </summary>
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
}
