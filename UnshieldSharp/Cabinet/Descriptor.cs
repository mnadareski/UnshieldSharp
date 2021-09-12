using System;
using System.IO;
using static UnshieldSharp.Cabinet.Constants;

namespace UnshieldSharp.Cabinet
{
    public class Descriptor
    {
        public uint StringsOffset { get; private set; }
        public uint Reserved0 { get; private set; }
        public uint ComponentListOffset { get; private set; }
        public uint FileTableOffset { get; private set; }
        public uint Reserved1 { get; private set; }
        public uint FileTableSize { get; private set; }
        public uint FileTableSize2 { get; private set; }
        public ushort DirectoryCount { get; private set; }
        public uint Reserved2 { get; private set; }
        public ushort Reserved3 { get; private set; }
        public uint Reserved4 { get; private set; }
        public uint FileCount { get; private set; }
        public uint FileTableOffset2 { get; private set; }
        public ushort ComponentTableInfoCount { get; private set; }
        public uint ComponentTableOffset { get; private set; }
        public uint Reserved5 { get; private set; }
        public uint Reserved6 { get; private set; }
        public uint[] FileGroupOffsets { get; private set; } = new uint[MAX_FILE_GROUP_COUNT];
        public uint[] ComponentOffsets { get; private set; } = new uint[MAX_COMPONENT_COUNT];
        public uint STypesOffset { get; private set; }
        public uint STableOffset { get; private set; }
        public uint Reserved7 { get; private set; }
        public uint Reserved8 { get; private set; }

        /// <summary>
        /// Create a new Descriptor from a Stream and CommonHeader
        /// </summary>
        public static Descriptor Create(Stream stream, CommonHeader commonHeader)
        {
            stream.Seek(commonHeader.DescriptorOffset, SeekOrigin.Begin);

            var descriptor = new Descriptor();

            descriptor.StringsOffset = stream.ReadUInt32();
            descriptor.Reserved0 = stream.ReadUInt32();
            descriptor.ComponentListOffset = stream.ReadUInt32();
            descriptor.FileTableOffset = stream.ReadUInt32();
            descriptor.Reserved1 = stream.ReadUInt32();
            descriptor.FileTableSize = stream.ReadUInt32();
            descriptor.FileTableSize2 = stream.ReadUInt32();
            descriptor.DirectoryCount = stream.ReadUInt16();
            descriptor.Reserved2 = stream.ReadUInt32();
            descriptor.Reserved3 = stream.ReadUInt16();
            descriptor.Reserved4 = stream.ReadUInt32();
            descriptor.FileCount = stream.ReadUInt32();
            descriptor.FileTableOffset2 = stream.ReadUInt32();
            if (descriptor.FileTableSize != descriptor.FileTableSize2)
                Console.Error.WriteLine("File table sizes do not match");

            descriptor.ComponentTableInfoCount = stream.ReadUInt16();
            descriptor.ComponentTableOffset = stream.ReadUInt32();
            descriptor.Reserved5 = stream.ReadUInt32();
            descriptor.Reserved6 = stream.ReadUInt32();

            for (int i = 0; i < MAX_FILE_GROUP_COUNT; i++)
            {
                descriptor.FileGroupOffsets[i] = stream.ReadUInt32();
            }
            
            for (int i = 0; i < MAX_COMPONENT_COUNT; i++)
            {
                descriptor.ComponentOffsets[i] = stream.ReadUInt32();
            }

            descriptor.STypesOffset = stream.ReadUInt32();
            descriptor.STableOffset = stream.ReadUInt32();
            descriptor.Reserved7 = stream.ReadUInt32();
            descriptor.Reserved8 = stream.ReadUInt32();

            return descriptor;
        }
    }
}
