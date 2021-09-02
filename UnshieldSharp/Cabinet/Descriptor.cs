using System;
using System.IO;
using static UnshieldSharp.Cabinet.Constants;

namespace UnshieldSharp.Cabinet
{
    public class Descriptor
    {
        public uint FileTableOffset { get; private set; }             /* 0c */
        public uint FileTableSize { get; private set; }               /* 14 */
        public uint FileTableSize2 { get; private set; }              /* 18 */
        public uint DirectoryCount { get; private set; }              /* 1c */
        public uint FileCount { get; private set; }                   /* 28 */
        public uint FileTableOffset2 { get; private set; }            /* 2c */

        public uint[] FileGroupOffsets { get; private set; } = new uint[MAX_FILE_GROUP_COUNT];  /* 0x3e  */
        public uint[] ComponentOffsets { get; private set; } = new uint[MAX_COMPONENT_COUNT];   /* 0x15a */
    
        /// <summary>
        /// Create a new Descriptor from a Stream and offset
        /// </summary>
        public static Descriptor Create(Stream stream, uint offset)
        {
            var descriptor = new Descriptor();
            int p = (int)offset + 0xC;
            stream.Seek(p, SeekOrigin.Begin);

            descriptor.FileTableOffset = stream.ReadUInt32();
            stream.Seek(4, SeekOrigin.Current);
            descriptor.FileTableSize = stream.ReadUInt32();
            descriptor.FileTableSize2 = stream.ReadUInt32();
            descriptor.DirectoryCount = stream.ReadUInt32();
            stream.Seek(8, SeekOrigin.Current);
            descriptor.FileCount = stream.ReadUInt32();
            descriptor.FileTableOffset2 = stream.ReadUInt32();
            if (descriptor.FileTableSize != descriptor.FileTableSize2)
                Console.Error.WriteLine("File table sizes do not match");

            stream.Seek(0xE, SeekOrigin.Current);
            for (int i = 0; i < MAX_FILE_GROUP_COUNT; i++)
            {
                descriptor.FileGroupOffsets[i] = stream.ReadUInt32();
            }
            
            for (int i = 0; i < MAX_COMPONENT_COUNT; i++)
            {
                descriptor.ComponentOffsets[i] = descriptor.FileGroupOffsets[i] = stream.ReadUInt32();
            }

            return descriptor;
        }
    }
}
