using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnshieldSharp
{
    public class Header
    {
        public Header Next { get; set; }
        public int Index { get; set; }
        public Stream Data { get; set; } 
        public int MajorVersion { get; set; }

        // Shortcuts
        public CommonHeader CommonHeader { get; private set; } = new CommonHeader();
        public CabDescriptor CabDescriptor { get; private set; } = new CabDescriptor();
        public uint[] FileTable { get; private set; }
        public int FileTablePointer { get; private set; }
        public FileDescriptor[] FileDescriptors { get; set; }
        public int FileDescriptorsPointer { get; private set; }

        public int ComponentCount { get; private set; }
        public UnshieldComponent[] Components { get; private set; }
        public int ComponentsPointer { get; private set; }

        public int FileGroupCount { get; private set; }
        public UnshieldFileGroup[] FileGroups { get; private set; }
        public int FileGroupsCounter { get; private set; }

        /// <summary>
        /// Create a new Header from a stream, a version, and an index
        /// </summary>
        public static Header Create(Stream stream, int version, int index)
        {
            var header = new Header
            {
                Index = index,
            };
            
            if (stream.Length < 4)
            {
                Console.Error.WriteLine($"Header file {index} is too small");
                return null;
            }

            header.Data = stream;
            if (!header.GetCommmonHeader())
            {
                Console.Error.WriteLine($"Failed to read common header from header file {index}");
                return null;
            }

            if (version != -1)
            {
                header.MajorVersion = version;
            }
            else if ((header.CommonHeader.Version >> 24) == 1)
            {
                header.MajorVersion = (int)((header.CommonHeader.Version >> 12) & 0xf);
            }
            else if ((header.CommonHeader.Version >> 24) == 2 || (header.CommonHeader.Version >> 24) == 4)
            {
                header.MajorVersion = (int)(header.CommonHeader.Version & 0xffff);
                if (header.MajorVersion != 0)
                    header.MajorVersion /= 100;
            }

            if (!header.GetCabDescriptor())
            {
                Console.Error.WriteLine($"Failed to read CAB descriptor from header file {index}");
                return null;
            }

            if (!header.GetFileTable())
            {
                Console.Error.WriteLine($"Failed to read file table from header file {index}");
                return null;
            }

            if (!header.GetComponents())
            {
                Console.Error.WriteLine($"Failed to read components from header file {index}");
                return null;
            }

            if (!header.GetFileGroups())
            {
                Console.Error.WriteLine($"Failed to read file groups from header file {index}");
                return null;
            }

            return header;
        }

        /// <summary>
        /// Populate the CabDescriptor from header data
        /// </summary>
        public bool GetCabDescriptor()
        {
            if (this.CommonHeader.CabDescriptorSize > 0)
            {
                this.CabDescriptor = CabDescriptor.Create(this.Data, this.CommonHeader.CabDescriptorOffset);
                return true;
            }
            else
            {
                Console.Error.WriteLine("No CAB descriptor available!");
                return false;
            }
        }

        /// <summary>
        /// Populate the CommonHeader from header data
        /// </summary>
        public bool GetCommmonHeader()
        {
            this.CommonHeader = CommonHeader.Create(this.Data);
            return this.CommonHeader != default;
        }

        /// <summary>
        /// Populate the component list from header data
        /// </summary>
        public bool GetComponents()
        {
            int count = 0;
            this.Components = new UnshieldComponent[Constants.MAX_COMPONENT_COUNT];

            for (int i = 0; i < Constants.MAX_COMPONENT_COUNT; i++)
            {
                if (this.CabDescriptor.ComponentOffsets[i] <= 0)
                    continue;

                var list = new OffsetList(this.CabDescriptor.ComponentOffsets[i]);
                while (list.NextOffset > 0)
                {
                    int p = GetDataOffset(list.NextOffset);
                    list = OffsetList.Create(this.Data, p);
                    this.Components[count++] = UnshieldComponent.Create(this, list.DescriptorOffset);
                }
            }

            this.ComponentCount = count;
            return true;
        }

        /// <summary>
        /// Get the real data offset
        /// </summary>
        public int GetDataOffset(uint offset)
        {
            if (offset > 0)
                return (int)(this.CommonHeader.CabDescriptorOffset + offset);
            else
                return -1;
        }

        /// <summary>
        /// Populate the file group list from header data
        /// </summary>
        public bool GetFileGroups()
        {
            int count = 0;
            this.FileGroups = new UnshieldFileGroup[Constants.MAX_FILE_GROUP_COUNT];

            for (int i = 0; i < Constants.MAX_FILE_GROUP_COUNT; i++)
            {
                if (this.CabDescriptor.FileGroupOffsets[i] <= 0)
                    continue;

                var list = new OffsetList(this.CabDescriptor.FileGroupOffsets[i]);
                while (list.NextOffset > 0)
                {
                    int p = GetDataOffset(list.NextOffset);
                    list = OffsetList.Create(this.Data, p);
                    this.FileGroups[count++] = UnshieldFileGroup.Create(this, list.DescriptorOffset);
                }
            }

            this.FileGroupCount = count;
            return true;
        }

        /// <summary>
        /// Populate the file table from header data
        /// </summary>
        public bool GetFileTable()
        {
            int p = (int)(this.CommonHeader.CabDescriptorOffset + this.CabDescriptor.FileTableOffset);
            int count = (int)(this.CabDescriptor.DirectoryCount + this.CabDescriptor.FileCount);

            this.FileTable = new uint[count];
            this.Data.Seek(p, SeekOrigin.Begin);
            for (int i = 0; i < count; i++)
            {
                this.FileTable[i] = this.Data.ReadUInt32();
            }

            return true;
        }

        /// <summary>
        /// Get the UInt32 at the given offset in the header data as a string
        /// </summary>
        public string GetString(uint offset)
        {
            this.Data.Seek(GetDataOffset(offset), SeekOrigin.Begin);
            return this.Data.ReadNullTerminatedString();
        }

        /// <summary>
        /// Convert a UInt32 read from a buffer to a string
        /// </summary>
        public static string GetUTF8String(Stream stream, int offset)
        {
            List<byte> buffer = new List<byte>();
            stream.Seek(offset, SeekOrigin.Begin);
            byte b;
            while ((b = (byte)stream.ReadByte()) != 0x00)
            {
                buffer.Add(b);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
