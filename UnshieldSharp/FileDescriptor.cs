using System.IO;

namespace UnshieldSharp
{
    public class FileDescriptor
    {
        public uint NameOffset { get; private set; }
        public uint DirectoryIndex { get; private set; }
        public FileDescriptorFlag Flags { get; set; }
        public ulong ExpandedSize { get; private set; }
        public ulong CompressedSize { get; private set; }
        public ulong DataOffset { get; private set; }
        public byte[] Md5 { get; private set; } = new byte[16];
        public ushort Volume { get; set; }
        public uint LinkPrevious { get; private set; }
        public uint LinkNext { get; private set; }
        public FileDescriptorLinkFlag LinkFlags { get; private set; }

        /// <summary>
        /// Create a new FileDescriptor from a header and an index
        /// </summary>
        public static FileDescriptor Create(Header header, int index)
        {
            var fd = new FileDescriptor();

            if (header.MajorVersion <= 5)
            {
                int p = (int)(header.CommonHeader.CabDescriptorOffset
                    + header.CabDescriptor.FileTableOffset
                    + header.FileTable[header.CabDescriptor.DirectoryCount + index]);

                header.Data.Seek(p, SeekOrigin.Begin);
                fd.Volume = (ushort)header.Index;
                fd.NameOffset = header.Data.ReadUInt32();
                fd.DirectoryIndex = header.Data.ReadUInt32();
                fd.Flags = (FileDescriptorFlag)header.Data.ReadUInt16();
                fd.ExpandedSize = header.Data.ReadUInt32();
                fd.CompressedSize = header.Data.ReadUInt32();
                header.Data.Seek(0x14, SeekOrigin.Current);
                fd.DataOffset = header.Data.ReadUInt32();

                if (header.MajorVersion == 5)
                    header.Data.Read(fd.Md5, 0, 0x10);
            }
            else
            {
                int p = (int)(header.CommonHeader.CabDescriptorOffset
                    + header.CabDescriptor.FileTableOffset
                    + header.CabDescriptor.FileTableOffset2
                    + index * 0x57);

                header.Data.Seek(p, SeekOrigin.Begin);
                fd.Flags = (FileDescriptorFlag)header.Data.ReadUInt16();
                fd.ExpandedSize = header.Data.ReadUInt64();
                fd.CompressedSize = header.Data.ReadUInt64();
                fd.DataOffset = header.Data.ReadUInt64();
                header.Data.Read(fd.Md5, 0, 0x10);
                header.Data.Seek(0x10, SeekOrigin.Current);
                fd.NameOffset = header.Data.ReadUInt32();
                fd.DirectoryIndex = header.Data.ReadUInt16();
                header.Data.Seek(0xC, SeekOrigin.Current);
                fd.LinkPrevious = header.Data.ReadUInt32();
                fd.LinkNext = header.Data.ReadUInt32();
                fd.LinkFlags = (FileDescriptorLinkFlag)header.Data.ReadUInt8();
                fd.Volume = header.Data.ReadUInt16();
            }
        
            return fd;
        }

    }
}
