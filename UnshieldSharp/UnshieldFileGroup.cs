using System.IO;

namespace UnshieldSharp
{
    public class UnshieldFileGroup
    {
        public string Name { get; private set; }
        public uint FirstFile { get; private set; }
        public uint LastFile { get; private set; }

        /// <summary>
        /// Create a new UnshieldFileGroup from a header and offset
        /// </summary>
        public static UnshieldFileGroup Create(Header header, uint offset)
        {
            int p = header.GetDataOffset(offset);
            header.Data.Seek(p, SeekOrigin.Begin);

            string name = header.GetString(header.Data.ReadUInt32());
            p += 4 + (header.MajorVersion <= 5 ? 0x48 : 0x12);
            header.Data.Seek(p, SeekOrigin.Begin);
            return new UnshieldFileGroup
            {
                Name = name,
                FirstFile = header.Data.ReadUInt32(),
                LastFile = header.Data.ReadUInt32(),
            };
        }
    }
}
