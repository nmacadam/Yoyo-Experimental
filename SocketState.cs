using System.Net.Sockets;

namespace Yoyo
{
    /// <summary>
    /// Base class for sending socket state info through async callbacks
    /// </summary>
    public class SocketState
    {
        public Socket WorkSocket = null;
        public ProtocolInfo Info;
        public const int BufferSize = 4096;
        public byte[] ReceiveBuffer = new byte[BufferSize];
    }
}
