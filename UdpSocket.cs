using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Yoyo
{
    public abstract class UdpSocket
    {
        private Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private const int _bufferSize = 8 * 1024;
        private State _state = new State();
        private EndPoint _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        private AsyncCallback _receive = null;

        protected Socket socket { get => _socket; set => _socket = value; }

        public class State
        {
            public byte[] buffer = new byte[_bufferSize];
        }

        public void Send(byte[] buffer)
        {
            _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, (arg) =>
            {
                State so = (State)arg.AsyncState;
                int bytes = _socket.EndSend(arg);
                Console.WriteLine("SEND: {0}", bytes);
            }, _state);
        }

        public void Receive()
        {
            _socket.BeginReceiveFrom(_state.buffer, 0, _bufferSize, SocketFlags.None, ref _remoteEndPoint, _receive = (arg) =>
            {
                State so = (State)arg.AsyncState;
                int bytes = _socket.EndReceiveFrom(arg, ref _remoteEndPoint);
                _socket.BeginReceiveFrom(so.buffer, 0, _bufferSize, SocketFlags.None, ref _remoteEndPoint, _receive, so);
                Console.WriteLine("RECV: {0}: {1}, {2}", _remoteEndPoint.ToString(), bytes, Encoding.ASCII.GetString(so.buffer, 0, bytes));
            }, _state);
        }
    }

    public class ServerSocket : UdpSocket
    {
        public ServerSocket(string address, int port)
        {
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
        }
    }

    public class ClientSocket : UdpSocket
    {
        public ClientSocket(string address, int port)
        {
            socket.Connect(IPAddress.Parse(address), port);
        }
    }
}
