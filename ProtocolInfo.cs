namespace Yoyo
{
    /// <summary>
    /// Manages messaging protocol parameters
    /// </summary>
    public class ProtocolInfo
    {
        private uint _sequenceNumber = 0;

        public uint Sequence => _sequenceNumber++;
    }
}
