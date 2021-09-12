using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class FileGroup
    {
        public uint NameOffset { get; private set; }
        public string Name { get; private set; }
        public uint ExpandedSize { get; private set; }
        public uint Reserved0 { get; private set; }
        public uint CompressedSize { get; private set; }
        public uint Reserved1 { get; private set; }
        public ushort Reserved2 { get; private set; }
        public ushort Attribute1 { get; private set; }
        public ushort Attribute2 { get; private set; }
        public uint FirstFile { get; private set; }
        public uint LastFile { get; private set; }
        public uint UnknownOffset { get; private set; }
        public uint Var4Offset { get; private set; }
        public uint Var1Offset { get; private set; }
        public uint HTTPLocationOffset { get; private set; }
        public uint FTPLocationOffset { get; private set; }
        public uint MiscOffset { get; private set; }
        public uint Var2Offset { get; private set; }
        public uint TargetDirectoryOffset { get; private set; }
        public ushort Reserved3 { get; private set; }
        public ushort Reserved4 { get; private set; }
        public ushort Reserved5 { get; private set; }
        public ushort Reserved6 { get; private set; }
        public ushort Reserved7 { get; private set; }

        /// <summary>
        /// Create a new FileGroup from a header and offset
        /// </summary>
        public static FileGroup Create(Header header, uint offset)
        {
            int dataOffset = header.GetDataOffset(offset);
            if (dataOffset < 0 || dataOffset >= header.Data.Length)
                return null;

            header.Data.Seek(dataOffset, SeekOrigin.Begin);

            FileGroup fileGroup = new FileGroup();

            fileGroup.NameOffset = header.Data.ReadUInt32();
            fileGroup.Name = header.GetString(fileGroup.NameOffset);
            fileGroup.ExpandedSize = header.Data.ReadUInt32();
            fileGroup.Reserved0 = header.Data.ReadUInt32();
            fileGroup.CompressedSize = header.Data.ReadUInt32();
            fileGroup.Reserved1 = header.Data.ReadUInt32();
            fileGroup.Reserved2 = header.Data.ReadUInt16();
            fileGroup.Attribute1 = header.Data.ReadUInt16();
            fileGroup.Attribute2 = header.Data.ReadUInt16();

            // TODO: Figure out what data lives in this area for V5 and below
            if (header.MajorVersion <= 5)
                header.Data.Seek(0x36, SeekOrigin.Current);

            fileGroup.FirstFile = header.Data.ReadUInt32();
            fileGroup.LastFile = header.Data.ReadUInt32();
            fileGroup.UnknownOffset = header.Data.ReadUInt32();
            fileGroup.Var4Offset = header.Data.ReadUInt32();
            fileGroup.Var1Offset = header.Data.ReadUInt32();
            fileGroup.HTTPLocationOffset = header.Data.ReadUInt32();
            fileGroup.FTPLocationOffset = header.Data.ReadUInt32();
            fileGroup.MiscOffset = header.Data.ReadUInt32();
            fileGroup.Var2Offset = header.Data.ReadUInt32();
            fileGroup.TargetDirectoryOffset = header.Data.ReadUInt32();
            fileGroup.Reserved3 = header.Data.ReadUInt16();
            fileGroup.Reserved4 = header.Data.ReadUInt16();
            fileGroup.Reserved5 = header.Data.ReadUInt16();
            fileGroup.Reserved6 = header.Data.ReadUInt16();
            fileGroup.Reserved7 = header.Data.ReadUInt16();

            return fileGroup;
        }
    }
}
