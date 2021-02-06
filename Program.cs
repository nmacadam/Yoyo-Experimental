using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yoyo
{
    // could do some special stuff to make byte conversion a lot faster
    public struct Header
    {
        public uint Protocol;
        public uint Sequence;
        public uint Ack;
        public uint AckBitfield;
    }

    public enum PacketType
    {
        Data = 0x0,
        Acknowledgment = 0x1,
    }

    public struct Received
    {
        public Header Header;
        public IPEndPoint Sender;
        public PacketType Type;
        public string Message;

        public override string ToString()
        {
            string bitfield = Convert.ToString(Header.AckBitfield, 2);
            return $"{Sender.Address} {Type} (Sequence:{Header.Sequence}; Ack:{Header.Ack}): '{Message}'\t{bitfield}";
        }
    }

    public abstract class NetBase
    {
        private readonly string version = "v0.1a";

        private readonly UInt32 _protocolId;
        private readonly byte[] _protocolIdBytes;

        private UInt32 _sequenceNumber = 0;
        private UInt32 _remoteSequenceNumber = 0;

        private Queue<uint> _ackHistory = new Queue<uint>();
        private int _historySize = 32;

        private UdpClient _client;

        protected uint protocolId => _protocolId;
        protected byte[] protocolIdBytes => _protocolIdBytes;

        protected UdpClient client { get => _client; set => _client = value; }

        protected NetBase()
        {
            string hashInput = "yoyo_networking_framework" + version;
            _protocolId = GetProtocolHash(hashInput);
            _protocolIdBytes = BitConverter.GetBytes(_protocolId);

            _client = new UdpClient();
        }

        private static uint GetProtocolHash(string inputString)
        {
            uint hash = 0;
            foreach (byte b in System.Text.Encoding.Unicode.GetBytes(inputString))
            {
                hash += b;
                hash += (hash << 10);
                hash ^= (hash >> 6);
            }
            
            hash += (hash << 3);
            hash ^= (hash >> 11);
            hash += (hash << 15);

            return (uint)(hash % 100000000);
        }

        // blocking!
        public Received Receive()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
            var result = _client.Receive(ref endPoint);

            if (!IsHeaderValid(result))
            {
                // discard
                return new Received();
            }

            uint sequenceNumber = GetSequenceNumber(result);

            if (sequenceNumber > _remoteSequenceNumber)
            {
                _remoteSequenceNumber = sequenceNumber;
            }

            uint ack = GetAck(result);
            uint ackBitfield = GetAckBitfield(result);

            // scan bitfield
            //if ((ackBitfield & (1 << (int)ack - 1)) != 0)
            //{
            //    // bit n is set; acknolwedge sequence number pack (sequence - n) if it is not already acked
            //    Console.WriteLine("Bitfield contains ack for #" + ack);
            //}
            //else
            //{
            //    Console.WriteLine("Bitfield DOES NOT contain ack for #" + ack);
            //}

            result = StripHeader(result);

            PacketType type = (PacketType)GetType(result);

            result = StripType(result);

            Received packet = new Received()
            {
                Header = new Header() { Protocol = _protocolId, Sequence = sequenceNumber, Ack = ack, AckBitfield = ackBitfield },
                Type = type,
                Message = Encoding.ASCII.GetString(result, 0, result.Length),
                Sender = endPoint
            };

            // could just enqueue the sequence number
            _ackHistory.Enqueue(_remoteSequenceNumber);
            if (_ackHistory.Count > _historySize) _ackHistory.Dequeue();

            return packet;
        }

        protected byte[] CreateHeader()
        {
            byte[] header = protocolIdBytes;

            // shouldn't really increment here
            var sequenceBytes = BitConverter.GetBytes(_sequenceNumber++);
            header = header.Concat(sequenceBytes).ToArray();

            var ackBytes = BitConverter.GetBytes(_remoteSequenceNumber);
            header = header.Concat(ackBytes).ToArray();

            var ackBitfield = CreateAckBitfield(_remoteSequenceNumber);
            header = header.Concat(BitConverter.GetBytes(ackBitfield)).ToArray();

            return header;
        }

        private uint CreateAckBitfield(uint remoteSequenceNumber)
        {
            uint startValue = 0x00000000;
            for (int i = 0; i < _ackHistory.Count; i++)
            {
                uint bitValue = _ackHistory.Contains((uint)i) ? (uint)1 : (uint)0;
                uint bitIndex = remoteSequenceNumber - (uint)i;
                startValue = startValue | (bitValue << (int)bitIndex);
            }

            return startValue;
        }

        private bool IsHeaderValid(byte[] buffer)
        {
            if (buffer.Length < protocolIdBytes.Length)
            {
                // too small to contain header
                return false;
            }

            byte[] protocolHeader = new byte[protocolIdBytes.Length];

            for (int i = 0; i < protocolHeader.Length; i++)
            {
                protocolHeader[i] = buffer[i];
            }

            if (BitConverter.ToUInt32(protocolHeader, 0) != protocolId)
            {
                // header does not match
                return false;
            }

            return true;
        }

        private uint GetSequenceNumber(byte[] buffer)
        {
            int offset = protocolIdBytes.Length;
            return BitConverter.ToUInt32(buffer, offset);
        }

        private uint GetAck(byte[] buffer)
        {
            int sequenceNumberSize = 4;
            int offset = protocolIdBytes.Length + sequenceNumberSize;
            return BitConverter.ToUInt32(buffer, offset);
        }

        private uint GetAckBitfield(byte[] buffer)
        {
            int sequenceNumberSize = 4;
            int ackSize = 4;
            int offset = protocolIdBytes.Length + sequenceNumberSize + ackSize;
            return BitConverter.ToUInt32(buffer, offset);
        }

        private uint GetType(byte[] headerlessBuffer)
        {
            //int typeIdSize = sizeof(UInt32);
            return BitConverter.ToUInt32(headerlessBuffer, 0);
        }

        private byte[] StripHeader(byte[] buffer)
        {
            int sequenceLength = 4;
            int ackLength = 4;
            int ackBitfieldLength = 4;
            return buffer.Skip(protocolIdBytes.Length + sequenceLength + ackLength + ackBitfieldLength).ToArray();
        }

        private byte[] StripType(byte[] buffer)
        {
            int typeIdSize = 4;
            return buffer.Skip(typeIdSize).ToArray();
        }

        //public async Task<Received> Receive()
        //{
        //    var result = await _client.ReceiveAsync();
        //    return new Received()
        //    {
        //        Message = Encoding.ASCII.GetString(result.Buffer, 0, result.Buffer.Length),
        //        Sender = result.RemoteEndPoint
        //    };
        //}
    }

    // Listener
    public class NetServer : NetBase
    {
        private IPEndPoint _listenOn;

        private Dictionary<IPEndPoint, double> _connections = new Dictionary<IPEndPoint, double>();

        private object _connectionsLock = new object();

        public NetServer() : this(new IPEndPoint(IPAddress.Any, 32123))
        {}

        public NetServer(IPEndPoint endpoint)
        {
            _listenOn = endpoint;
            client = new UdpClient(_listenOn);
        }

        public void Reply(PacketType type, string message, IPEndPoint endpoint)
        {
            //var datagram = Encoding.ASCII.GetBytes(message);
            //datagram = protocolIdBytes.Concat(datagram).ToArray();
            //client.Send(datagram, datagram.Length, endpoint);

            byte[] typeBytes = BitConverter.GetBytes((uint)type);
            var datagram = Encoding.ASCII.GetBytes(message);
            datagram = CreateHeader().Concat(typeBytes).Concat(datagram).ToArray();

            Console.WriteLine($"SERVER | -> {type}, {message}");
            client.Send(datagram, datagram.Length, endpoint);
        }

        public double GetLifespan(IPEndPoint endPoint)
        {
            double value;
            lock (_connectionsLock)
            {
                value = _connections[endPoint];
            }

            return value;
        }

        private void HandleConnection(IPEndPoint endPoint)
        {
            long duration = TimeSpan.FromSeconds(5.0).Ticks;

            long t = DateTime.Now.Ticks;

            long lastTicks = DateTime.Now.Ticks;

            while (GetLifespan(endPoint) < duration)
            {
                long ticks = DateTime.Now.Ticks;

                lock (_connectionsLock)
                {
                    _connections[endPoint] += ticks - lastTicks;
                }

                lastTicks = ticks;

                // No reason for this to be insanely accurate; let the thread take a break for a bit
                Thread.Sleep(50);
            }

            _connections.Remove(endPoint);
            Console.WriteLine($"SERVER | client {endPoint.Address} disconnected");
        }

        // move to thread
        public void Listen()
        {
            while (true)
            {
                var received = Receive();

                if (!_connections.ContainsKey(received.Sender))
                {
                    Console.WriteLine($"SERVER | client {received.Sender.Address} connected");
                    Thread connectionThread = new Thread(new ThreadStart(() => HandleConnection(received.Sender)));
                    connectionThread.Start();
                    lock (_connectionsLock)
                    {
                        _connections.Add(received.Sender, 0);
                    }
                }
                else
                {
                    lock (_connectionsLock)
                    {
                        _connections[received.Sender] = 0;
                    }
                }

                if (received.Sender == null) continue;

                Console.WriteLine($"SERVER | <- {received.ToString()}");
                
                Reply(PacketType.Acknowledgment, "", received.Sender);

                if (received.Message == "quit")
                    break;
            }
        }
    }

    // User
    public class NetClient : NetBase
    {
        private NetClient()
        {}

        public static NetClient ConnectTo(string hostname, int port)
        {
            var connection = new NetClient();
            connection.client.Connect(hostname, port);
            return connection;
        }

        public void Send(PacketType type, string message)
        {
            //byte[] header = protocolIdBytes;

            //var sequenceNumber = BitConverter.GetBytes(_sequenceNumber++);
            //header = header.Concat(sequenceNumber).ToArray();

            //var datagram = Encoding.ASCII.GetBytes(message);
            //datagram = header.Concat(datagram).ToArray();
            //client.Send(datagram, datagram.Length);

            byte[] typeBytes = BitConverter.GetBytes((uint)type);
            var datagram = Encoding.ASCII.GetBytes(message);
            datagram = CreateHeader().Concat(typeBytes).Concat(datagram).ToArray();
            Console.WriteLine($"CLIENT | -> {type}, {message}");
            client.Send(datagram, datagram.Length);
        }
    }

    class Program
    {
        public static void ServerThread()
        {
            NetServer server = new NetServer();
            server.Listen();
        }

        public static void ClientThread(NetClient client)
        {
            while (true)
            {
                try
                {
                    var received = client.Receive();
                    Console.WriteLine($"CLIENT | <- {received.ToString()}");
                    if (received.Message.Contains("quit"))
                        break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        static void Main(string[] args)
        {
            //UdpSocket s = new ServerSocket("127.0.0.1", 27000);
            //s.Receive();

            ////UdpSocket c = new ClientSocket("127.0.0.1", 27000);
            ////c.Send(Encoding.ASCII.GetBytes("TEST!"));

            //Console.ReadKey();

            //return;

            Thread serverThread = new Thread(new ThreadStart(ServerThread));

            var clientA = NetClient.ConnectTo("127.0.0.1", 32123);
            //var clientB = NetClient.ConnectTo("127.0.0.1", 32123);
            //var clientC = NetClient.ConnectTo("127.0.0.1", 32123);
            Thread clientThreadA = new Thread(new ThreadStart(() => ClientThread(clientA)));
            //Thread clientThreadB = new Thread(new ThreadStart(() => ClientThread(clientB)));
            //Thread clientThreadC = new Thread(new ThreadStart(() => ClientThread(clientC)));

            serverThread.Start();
            clientThreadA.Start();
            //clientThreadB.Start();
            //clientThreadC.Start();

            //string read;
            //read = Console.ReadLine();
            //Console.WriteLine($"CLIENT: sending '{read}'");
            //clientA.Send(read);

            //for (int i = 0; i < 100; i++)
            //{
            //    read = "This is message #" + i;
            //    Console.WriteLine($"CLIENT: sending '{read}'");
            //    clientA.Send(read);
            //    Thread.Sleep(10);
            //}

            string read;
            do
            {
                read = Console.ReadLine();
                Console.WriteLine($"CLIENT | sending '{read}'");
                clientA.Send(PacketType.Data, read);
                //clientB.Send(read);
                //clientC.Send(read);
            } while (read != "quit");

            serverThread.Join();
            clientThreadA.Join();
            //clientThreadB.Join();
            //clientThreadC.Join();


            //var server = new NetServer();

            //// start listening for messages and copy the messages back to the client
            //Task.Factory.StartNew(async () => {
            //    while (true)
            //    {
            //        var received = await server.Receive();
            //        Console.WriteLine($"SERVER: recieved '{received.Message}' from {received.Sender.Address}");
            //        server.Reply("server response to: " + received.Message, received.Sender);
            //        if (received.Message == "quit")
            //            break;
            //    }
            //});

            //// create a new client
            //var clientA = NetClient.ConnectTo("127.0.0.1", 32123);
            //var clientB = NetClient.ConnectTo("127.0.0.1", 32123);

            //// wait for reply messages from server and send them to console 
            //Task.Factory.StartNew(async () => {
            //    while (true)
            //    {
            //        try
            //        {
            //            var received = await clientA.Receive();
            //            Console.WriteLine($"CLIENT 1: recieved '{received.Message}'");
            //            if (received.Message.Contains("quit"))
            //                break;
            //        }
            //        catch (Exception ex)
            //        {
            //            Console.WriteLine(ex);
            //        }
            //    }
            //});

            //Task.Factory.StartNew(async () => {
            //    while (true)
            //    {
            //        try
            //        {
            //            var received = await clientB.Receive();
            //            Console.WriteLine($"CLIENT 2: recieved '{received.Message}'");
            //            if (received.Message.Contains("quit"))
            //                break;
            //        }
            //        catch (Exception ex)
            //        {
            //            Console.WriteLine(ex);
            //        }
            //    }
            //});

            //// type ahead :-)
            //string read;
            //do
            //{
            //    read = Console.ReadLine();
            //    Console.WriteLine($"CLIENT 1: sending '{read}'");
            //    Console.WriteLine($"CLIENT 2: sending '{read}'");
            //    clientA.Send(read);
            //    clientB.Send(read);
            //} while (read != "quit");
        }
    }
}
