﻿namespace UnshieldSharp
{
    public class FileDescriptor
    {
        public uint NameOffset;
        public uint DirectoryIndex;
        public ushort Flags;
        public ulong ExpandedSize;
        public ulong CompressedSize;
        public ulong DataOffset;
        public byte[] Md5 = new byte[16];
        public ushort Volume;
        public uint LinkPrevious;
        public uint LinkNext;
        public byte LinkFlags;
    }
}
