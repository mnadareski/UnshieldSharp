using System;

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
        /// Create a new OffsetList from a header and data offset
        /// </summary>
        public static OffsetList Create(Header header, int offset)
        {
            var list = new OffsetList();

            list.NameOffset = BitConverter.ToUInt32(header.Data, offset); offset += 4;
            list.DescriptorOffset = BitConverter.ToUInt32(header.Data, offset); offset += 4;
            list.NextOffset = BitConverter.ToUInt32(header.Data, offset); offset += 4;

            return list;
        }
    }
}
