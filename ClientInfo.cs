namespace Yoyo
{
    public interface IClientInfo
    {
        IInbox Inbox { get;  }
        int Id { get; set; }
    }

    /// <summary>
    /// Server-side client representation
    /// </summary>
    public class ClientInfo : IClientInfo
    {
        private ClientInbox _inbox;

        public int Id { get; set; }

        public IInbox Inbox => _inbox;

        public readonly TcpMessenger Messenger;

        public ClientInfo(int id)
        {
            _inbox = new ClientInbox(this, new ClientOutbox(Messenger));
            Id = id;
            Messenger = new TcpMessenger(Inbox, Id);
        }

    }
}
