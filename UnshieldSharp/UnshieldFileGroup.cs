using System;

namespace UnshieldSharp
{
    public class UnshieldFileGroup
    {
        public string Name { get; private set; }
        public uint FirstFile { get; private set; }
        public uint LastFile { get; private set; }

        /// <summary>
        /// Create a new UnshieldFileGroup from a header and data offset
        /// </summary>
        public static UnshieldFileGroup Create(Header header, uint offset)
        {
            var fileGroup = new UnshieldFileGroup();
            int p = header.GetDataOffset(offset);

            fileGroup.Name = header.GetString(BitConverter.ToUInt32(header.Data, p)); p += 4;
            p += header.MajorVersion <= 5 ? 0x48 : 0x12;
            fileGroup.FirstFile = BitConverter.ToUInt32(header.Data, p); p += 4;
            fileGroup.LastFile = BitConverter.ToUInt32(header.Data, p); p += 4;

            return fileGroup;
        }
    }
}
