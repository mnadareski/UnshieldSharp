using System;
using System.Collections.Generic;
using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class Header
    {
        #region Fields

        /// <summary>
        /// Reference to the next cabinet header
        /// </summary>
        public Header? Next { get; set; }

        /// <summary>
        /// Current cabinet header index
        /// </summary>
        public int Index { get; private set; }

        /// <summary>
        /// Stream representing the cabinet set
        /// </summary>
        public Stream? Data { get; private set; }

        /// <summary>
        /// Internal major version of the cabinet set
        /// </summary>
        public int MajorVersion { get; private set; }

        /// <summary>
        /// Common file header information
        /// </summary>
        public CommonHeader? CommonHeader { get; private set; }

        /// <summary>
        /// Cabinet file descriptor
        /// </summary>
        public Descriptor? Descriptor { get; private set; } = new Descriptor();

        /// <summary>
        /// File offset table
        /// </summary>
        public uint[]? FileOffsetTable { get; private set; }

        /// <summary>
        /// File descriptors table
        /// </summary>
        public FileDescriptor[]? FileDescriptors { get; set; }

        /// <summary>
        /// Set of components inside of the cabinet set
        /// </summary>
        public Component[]? Components { get; private set; }

        /// <summary>
        /// Number of components in the cabinet set
        /// </summary>
        public int ComponentCount => this.Components?.Length ?? 0;

        /// <summary>
        /// Set of file groups inside of the cabinet set
        /// </summary>
        public FileGroup[]? FileGroups { get; private set; }

        /// <summary>
        /// Number of file groups in the cabinet set
        /// </summary>
        public int FileGroupCount => this.FileGroups?.Length ?? 0;

        #endregion

        /// <summary>
        /// Create a new Header from a stream, a version, and an index
        /// </summary>
        public static Header? Create(Stream stream, int version, int index)
        {
            var header = new Header { Index = index };
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

            header.MajorVersion = version != -1 ? version : header.CommonHeader?.MajorVersion ?? 0;

            if (!header.GetDescriptor())
            {
                Console.Error.WriteLine($"Failed to read CAB descriptor from header file {index}");
                return null;
            }

            header.GetFileOffsetTable();
            header.GetComponents();
            header.GetFileGroups();

            return header;
        }

        #region Helpers

        /// <summary>
        /// Get the real data offset
        /// </summary>
        public int GetDataOffset(uint offset)
        {
            if (offset > 0)
                return (int)(this.CommonHeader!.DescriptorOffset + offset);
            else
                return -1;
        }

        /// <summary>
        /// Get the UInt32 at the given offset in the header data as a string
        /// </summary>
        public string GetString(uint offset)
        {
            int dataOffset = GetDataOffset(offset);
            if (dataOffset <= 0)
                return string.Empty;

            long originalPosition = this.Data!.Position;
            this.Data.Seek(dataOffset, SeekOrigin.Begin);
            string str = this.Data.ReadNullTerminatedString();
            this.Data.Seek(originalPosition, SeekOrigin.Begin);
            return str;
        }

        /// <summary>
        /// Populate the CommonHeader from header data
        /// </summary>
        private bool GetCommmonHeader()
        {
            this.CommonHeader = CommonHeader.Create(this.Data!);
            return this.CommonHeader != default;
        }

        /// <summary>
        /// Populate the Descriptor from header data
        /// </summary>
        private bool GetDescriptor()
        {
            this.Descriptor = Descriptor.Create(this.Data!, this.CommonHeader!);
            return this.Descriptor != default;
        }

        /// <summary>
        /// Populate the component list from header data
        /// </summary>
        private void GetComponents()
        {
            var tempComponents = new List<Component>();
            for (int i = 0; i < this.Descriptor!.ComponentOffsets.Length; i++)
            {
                if (this.Descriptor.ComponentOffsets[i] <= 0)
                    continue;

                var list = new OffsetList(this.Descriptor.ComponentOffsets[i]);
                while (list.NextOffset > 0)
                {
                    int dataOffset = GetDataOffset(list.NextOffset);
                    if (dataOffset <= 0)
                        break;

                    list = OffsetList.Create(this.Data!, dataOffset);
                    var component = Component.Create(this, list.DescriptorOffset);
                    if (component == null)
                        break;

                    tempComponents.Add(component);
                }
            }

            this.Components = tempComponents.ToArray();
        }

        /// <summary>
        /// Populate the file group list from header data
        /// </summary>
        private void GetFileGroups()
        {
            var tempFileGroups = new List<FileGroup>();
            for (int i = 0; i < this.Descriptor!.FileGroupOffsets.Length; i++)
            {
                if (this.Descriptor.FileGroupOffsets[i] <= 0)
                    continue;

                var list = new OffsetList(this.Descriptor.FileGroupOffsets[i]);
                while (list.NextOffset > 0)
                {
                    int dataOffset = GetDataOffset(list.NextOffset);
                    if (dataOffset <= 0)
                        break;

                    list = OffsetList.Create(this.Data!, dataOffset);
                    var fileGroup = FileGroup.Create(this, list.DescriptorOffset);
                    if (fileGroup == null)
                        break;

                    tempFileGroups.Add(fileGroup);
                }
            }

            this.FileGroups = tempFileGroups.ToArray();
        }

        /// <summary>
        /// Populate the file offset table from header data
        /// </summary>
        private void GetFileOffsetTable()
        {
            int fileTableOffset = GetDataOffset(this.Descriptor!.FileTableOffset);
            int count = (int)(this.Descriptor.DirectoryCount + this.Descriptor.FileCount);

            this.FileOffsetTable = new uint[count];
            this.Data!.Seek(fileTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < count; i++)
            {
                this.FileOffsetTable[i] = this.Data.ReadUInt32();
            }
        }

        #endregion
    }
}
