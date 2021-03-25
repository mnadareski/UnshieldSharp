using System;

namespace UnshieldSharp
{
    public class CabDescriptor
    {
        public uint FileTableOffset { get; set; }             /* 0c */
        public uint FileTableSize { get; set; }               /* 14 */
        public uint FileTableSize2 { get; set; }              /* 18 */
        public uint DirectoryCount { get; set; }              /* 1c */
        public uint FileCount { get; set; }                   /* 28 */
        public uint FileTableOffset2 { get; set; }            /* 2c */

        public uint[] FileGroupOffsets { get; set; } = new uint[Constants.MAX_FILE_GROUP_COUNT];  /* 0x3e  */
        public uint[] ComponentOffsets { get; set; } = new uint[Constants.MAX_COMPONENT_COUNT];   /* 0x15a */
    
        /// <summary>
        /// Create a new CabDescriptor from a header and data offset
        /// </summary>
        public static CabDescriptor Create(Header header, uint offset)
        {
            var descriptor = new CabDescriptor();
            int p = (int)offset + 0xc;

            descriptor.FileTableOffset = BitConverter.ToUInt32(header.Data, p); p += 4;
            p += 4;
            descriptor.FileTableSize = BitConverter.ToUInt32(header.Data, p); p += 4;
            descriptor.FileTableSize2 = BitConverter.ToUInt32(header.Data, p); p += 4;
            descriptor.DirectoryCount = BitConverter.ToUInt32(header.Data, p); p += 4;
            p += 8;
            descriptor.FileCount = BitConverter.ToUInt32(header.Data, p); p += 4;
            descriptor.FileTableOffset2 = BitConverter.ToUInt32(header.Data, p); p += 4;

            // assert((p - (header->data + header->common.cab_descriptor_offset)) == 0x30);

            if (descriptor.FileTableSize != descriptor.FileTableSize2)
                Console.Error.WriteLine("File table sizes do not match");

            /*
            unshield_trace("Cabinet descriptor: %08x %08x %08x %08x",
                header->cab.file_table_offset,
                header->cab.file_table_size,
                header->cab.file_table_size2,
                header->cab.file_table_offset2
                );

            unshield_trace("Directory count: %i", header->cab.directory_count);
            unshield_trace("File count: %i", header->cab.file_count);
            */

            p += 0xe;

            for (int i = 0; i < Constants.MAX_FILE_GROUP_COUNT; i++)
            {
                descriptor.FileGroupOffsets[i] = BitConverter.ToUInt32(header.Data, p); p += 4;
            }
            
            for (int i = 0; i < Constants.MAX_COMPONENT_COUNT; i++)
            {
                descriptor.ComponentOffsets[i] = descriptor.FileGroupOffsets[i] = BitConverter.ToUInt32(header.Data, p); p += 4;
            }

            return descriptor;
        }
    }
}
