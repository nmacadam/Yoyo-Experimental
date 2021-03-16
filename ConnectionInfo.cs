using System.Net.Sockets;

namespace Yoyo
{
    /// <summary>
    /// Server-side representation of a client connection
    /// </summary>
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
}
