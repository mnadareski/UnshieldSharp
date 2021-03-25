using System;

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
        /// Create a new FileDescriptor from a header, a version, and an index
        /// </summary>
        public static FileDescriptor Create(Header header, int version, int index)
        {
            var fd = new FileDescriptor();

            if (version <= 5)
            {
                int p = (int)(header.CommonHeader.CabDescriptorOffset
                    + header.CabDescriptor.FileTableOffset
                    + header.FileTable[header.CabDescriptor.DirectoryCount + index]);

                // unshield_trace("File descriptor offset %i: %08x", index, p - header->data);

                fd.Volume = (ushort)header.Index;

                fd.NameOffset = BitConverter.ToUInt32(header.Data, p); p += 4;
                fd.DirectoryIndex = BitConverter.ToUInt32(header.Data, p); p += 4;

                fd.Flags = (FileDescriptorFlag)BitConverter.ToUInt16(header.Data, p); p += 2;

                fd.ExpandedSize = BitConverter.ToUInt32(header.Data, p); p += 4;
                fd.CompressedSize = BitConverter.ToUInt32(header.Data, p); p += 4;
                p += 0x14;
                fd.DataOffset = BitConverter.ToUInt32(header.Data, p); p += 4;

                /*
                unshield_trace("Name offset:      %08x", fd->name_offset);
                unshield_trace("Directory index:  %08x", fd->directory_index);
                unshield_trace("Flags:            %04x", fd->flags);
                unshield_trace("Expanded size:    %08x", fd->expanded_size);
                unshield_trace("Compressed size:  %08x", fd->compressed_size);
                unshield_trace("Data offset:      %08x", fd->data_offset);
                */

                if (header.MajorVersion == 5)
                {
                    Array.Copy(header.Data, p, fd.Md5, 0, 0x10);
                    // assert((p - saved_p) == 0x3a);
                }
            }
            else
            {
                int p = (int)(header.CommonHeader.CabDescriptorOffset
                    + header.CabDescriptor.FileTableOffset
                    + header.CabDescriptor.FileTableOffset2
                    + index * 0x57);

                // unshield_trace("File descriptor offset: %08x", p - header->data);

                fd.Flags = (FileDescriptorFlag)BitConverter.ToUInt16(header.Data, p); p += 2;
                fd.ExpandedSize = BitConverter.ToUInt64(header.Data, p); p += 8;
                fd.CompressedSize = BitConverter.ToUInt64(header.Data, p); p += 8;
                fd.DataOffset = BitConverter.ToUInt64(header.Data, p); p += 8;
                Array.Copy(header.Data, p, fd.Md5, 0, 0x10); p += 0x10;
                p += 0x10;
                fd.NameOffset = BitConverter.ToUInt32(header.Data, p); p += 4;
                fd.DirectoryIndex = BitConverter.ToUInt16(header.Data, p); p += 2;

                // assert((p - saved_p) == 0x40);

                p += 0xc;
                fd.LinkPrevious = BitConverter.ToUInt32(header.Data, p); p += 4;
                fd.LinkNext = BitConverter.ToUInt32(header.Data, p); p += 4;
                fd.LinkFlags = (FileDescriptorLinkFlag)header.Data[p]; p++;

                /*
                if (fd->link_flags != LINK_NONE)
                {
                    unshield_trace("Link: previous=%i, next=%i, flags=%i",
                    fd->link_previous, fd->link_next, fd->link_flags);
                }
                */

                fd.Volume = BitConverter.ToUInt16(header.Data, p); p += 2;

                // assert((p - saved_p) == 0x57);
            }
        
            return fd;
        }

    }
}
