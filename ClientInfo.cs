namespace Yoyo
{
    /// <summary>
    /// Server-side client representation
    /// </summary>
    public class ClientInfo
    {
        public int Id;
        public TcpMessenger Messenger;

        public ClientInfo(int id)
        {
            Id = id;
            Messenger = new TcpMessenger(id);
        }
    }
}
