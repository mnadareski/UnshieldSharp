using System.IO;

namespace UnshieldSharp
{
    public class OffsetList
    {
        public uint NameOffset { get; private set; }
        public uint DescriptorOffset { get; private set; }
        public uint NextOffset { get; private set; }

        public OffsetList(uint nextOffset = 0)
        {
            NextOffset = nextOffset;
        }

        /// <summary>
        /// Create a new OffsetList from a stream and offset
        /// </summary>
        public static OffsetList Create(Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            return new OffsetList
            {
                NameOffset = stream.ReadUInt32(),
                DescriptorOffset = stream.ReadUInt32(),
                NextOffset = stream.ReadUInt32(),
            };
        }
    }
}
