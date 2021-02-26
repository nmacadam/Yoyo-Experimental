namespace Yoyo
{
    public interface IClient
    {
        ClientMessenger Messenger { get; }
        IClientInfo Info { get; }
    }

    public interface IClientInfo
    {
        //IInbox Inbox { get;  }
        int Id { get; set; }
    }

    /// <summary>
    /// Server-side client representation
    /// </summary>
    public class ClientInfo : IClient, IClientInfo
    {
        private ClientMessenger _messenger;
        //private ClientInbox _inbox;

        public int Id { get; set; }

        public ClientMessenger Messenger => _messenger;

        public IClientInfo Info => this;

        //public IInbox Inbox => _inbox;

        //public readonly TcpMessenger Messenger;

        public ClientInfo(int id)
        {
            Id = id;
            _messenger = new ClientMessenger(this);
            //ClientOutbox outbox = null;
            //_inbox = new ClientInbox(this, outbox);
            //Id = id;
            //Messenger = new TcpMessenger(Inbox, Id);
            //outbox = new ClientOutbox(Messenger);
        }
    }
}
