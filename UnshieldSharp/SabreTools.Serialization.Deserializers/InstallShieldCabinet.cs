using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SabreTools.IO.Extensions;
using SabreTools.Models.InstallShieldCabinet;
using static SabreTools.Models.InstallShieldCabinet.Constants;

namespace SabreTools.Serialization.Deserializers
{
    public class InstallShieldCabinet : BaseBinaryDeserializer<Cabinet>
    {
        /// <inheritdoc/>
        public override Cabinet? Deserialize(Stream? data)
        {
            // If the data is invalid
            if (data == null || !data.CanRead)
                return null;

            try
            {
                // Cache the current offset
                long initialOffset = data.Position;

                // Create a new cabinet to fill
                var cabinet = new Cabinet();

                #region Common Header

                // Try to parse the cabinet header
                var commonHeader = ParseCommonHeader(data);
                if (commonHeader.Signature != SignatureString)
                    return null;

                // Set the cabinet header
                cabinet.CommonHeader = commonHeader;

                #endregion

                // Get the major version
                int majorVersion = commonHeader.GetMajorVersion();

                #region Volume Header

                // Set the volume header
                cabinet.VolumeHeader = ParseVolumeHeader(data, majorVersion);

                #endregion

                #region Descriptor

                // If the descriptor does not exist
                if (commonHeader.DescriptorSize == 0)
                    return cabinet;

                // Get the descriptor offset
                long descriptorOffset = initialOffset + commonHeader.DescriptorOffset;
                if (descriptorOffset < initialOffset || descriptorOffset >= data.Length)
                    return null;

                // Seek to the descriptor
                data.Seek(descriptorOffset, SeekOrigin.Begin);

                // Set the descriptor
                cabinet.Descriptor = ParseDescriptor(data);

                #endregion

                #region File Descriptor Offsets

                // Get the file table offset
                long fileTableOffset = descriptorOffset + cabinet.Descriptor.FileTableOffset;
                if (fileTableOffset < initialOffset || fileTableOffset >= data.Length)
                    return null;

                // Seek to the file table
                data.Seek(fileTableOffset, SeekOrigin.Begin);

                // Get the number of file table items
                uint fileTableItems;
                if (majorVersion <= 5)
                    fileTableItems = cabinet.Descriptor.DirectoryCount + cabinet.Descriptor.FileCount;
                else
                    fileTableItems = cabinet.Descriptor.DirectoryCount;

                // Create and fill the file table
                cabinet.FileDescriptorOffsets = new uint[fileTableItems];
                for (int i = 0; i < cabinet.FileDescriptorOffsets.Length; i++)
                {
                    cabinet.FileDescriptorOffsets[i] = data.ReadUInt32LittleEndian();
                }

                #endregion

                #region Directory Descriptors

                // Create and fill the directory descriptors
                cabinet.DirectoryNames = new string[cabinet.Descriptor.DirectoryCount];
                for (int i = 0; i < cabinet.Descriptor.DirectoryCount; i++)
                {
                    // Get the directory descriptor offset
                    long offset = descriptorOffset
                        + cabinet.Descriptor.FileTableOffset
                        + cabinet.FileDescriptorOffsets[i];

                    // If we have an invalid offset
                    if (offset < initialOffset || offset >= data.Length)
                        continue;

                    // Seek to the file descriptor offset
                    data.Seek(offset, SeekOrigin.Begin);

                    // Create and add the file descriptor
                    string? directoryName = ParseDirectoryName(data, majorVersion);
                    if (directoryName != null)
                        cabinet.DirectoryNames[i] = directoryName;
                }

                #endregion

                #region File Descriptors

                // Create and fill the file descriptors
                cabinet.FileDescriptors = new FileDescriptor[cabinet.Descriptor.FileCount];
                for (int i = 0; i < cabinet.Descriptor.FileCount; i++)
                {
                    // Get the file descriptor offset
                    long offset;
                    if (majorVersion <= 5)
                    {
                        offset = descriptorOffset
                            + cabinet.Descriptor.FileTableOffset
                            + cabinet.FileDescriptorOffsets[cabinet.Descriptor.DirectoryCount + i];
                    }
                    else
                    {
                        offset = descriptorOffset
                            + cabinet.Descriptor.FileTableOffset
                            + cabinet.Descriptor.FileTableOffset2
                            + (uint)(i * 0x57);
                    }

                    // If we have an invalid offset
                    if (offset < initialOffset || offset >= data.Length)
                        continue;

                    // Seek to the file descriptor offset
                    data.Seek(offset, SeekOrigin.Begin);

                    // Create and add the file descriptor
                    cabinet.FileDescriptors[i] = ParseFileDescriptor(data,
                        majorVersion,
                        descriptorOffset + cabinet.Descriptor.FileTableOffset);
                }

                #endregion

                #region File Group Offsets

                // Create and fill the file group offsets
                cabinet.FileGroupOffsets = new Dictionary<long, OffsetList?>();
                for (int i = 0; i < (cabinet.Descriptor.FileGroupOffsets?.Length ?? 0); i++)
                {
                    // Get the file group offset
                    long offset = cabinet.Descriptor.FileGroupOffsets![i];
                    if (offset == 0)
                        continue;

                    // Adjust the file group offset
                    offset += descriptorOffset;
                    if (offset < initialOffset || offset >= data.Length)
                        continue;

                    // Seek to the file group offset
                    data.Seek(offset, SeekOrigin.Begin);

                    // Create and add the offset
                    OffsetList offsetList = ParseOffsetList(data, majorVersion, descriptorOffset);
                    cabinet.FileGroupOffsets[offset] = offsetList;

                    // If we have a nonzero next offset
                    long nextOffset = offsetList.NextOffset;
                    while (nextOffset != 0)
                    {
                        // Get the next offset to read
                        long internalOffset = descriptorOffset + nextOffset;

                        // Seek to the file group offset
                        data.Seek(internalOffset, SeekOrigin.Begin);

                        // Create and add the offset
                        offsetList = ParseOffsetList(data, majorVersion, descriptorOffset);
                        cabinet.FileGroupOffsets[nextOffset] = offsetList;

                        // Set the next offset
                        nextOffset = offsetList.NextOffset;
                    }
                }

                #endregion

                #region File Groups

                // Create the file groups array
                cabinet.FileGroups = new FileGroup[cabinet.FileGroupOffsets.Count];

                // Create and fill the file groups
                int fileGroupId = 0;
                foreach (var kvp in cabinet.FileGroupOffsets)
                {
                    // Get the offset
                    OffsetList? list = kvp.Value;
                    if (list == null)
                    {
                        fileGroupId++;
                        continue;
                    }

                    // If we have an invalid offset
                    if (list.DescriptorOffset <= 0)
                    {
                        fileGroupId++;
                        continue;
                    }

                    /// Seek to the file group
                    data.Seek(descriptorOffset + list.DescriptorOffset, SeekOrigin.Begin);

                    // Add the file group
                    cabinet.FileGroups[fileGroupId++] = ParseFileGroup(data, majorVersion, descriptorOffset);
                }

                #endregion

                #region Component Offsets

                // Create and fill the component offsets
                cabinet.ComponentOffsets = new Dictionary<long, OffsetList?>();
                for (int i = 0; i < (cabinet.Descriptor.ComponentOffsets?.Length ?? 0); i++)
                {
                    // Get the component offset
                    long offset = cabinet.Descriptor.ComponentOffsets![i];
                    if (offset == 0)
                        continue;

                    // Adjust the component offset
                    offset += descriptorOffset;
                    if (offset < initialOffset || offset >= data.Length)
                        continue;

                    // Seek to the component offset
                    data.Seek(offset, SeekOrigin.Begin);

                    // Create and add the offset
                    OffsetList offsetList = ParseOffsetList(data, majorVersion, descriptorOffset);
                    cabinet.ComponentOffsets[cabinet.Descriptor.ComponentOffsets[i]] = offsetList;

                    // If we have a nonzero next offset
                    long nextOffset = offsetList.NextOffset;
                    while (nextOffset != 0)
                    {
                        // Get the next offset to read
                        long internalOffset = descriptorOffset + nextOffset;

                        // Seek to the file group offset
                        data.Seek(internalOffset, SeekOrigin.Begin);

                        // Create and add the offset
                        offsetList = ParseOffsetList(data, majorVersion, descriptorOffset);
                        cabinet.ComponentOffsets[nextOffset] = offsetList;

                        // Set the next offset
                        nextOffset = offsetList.NextOffset;
                    }
                }

                #endregion

                #region Components

                // Create the components array
                cabinet.Components = new Component[cabinet.ComponentOffsets.Count];

                // Create and fill the components
                int componentId = 0;
                foreach (KeyValuePair<long, OffsetList?> kvp in cabinet.ComponentOffsets)
                {
                    // Get the offset
                    OffsetList? list = kvp.Value;
                    if (list == null)
                    {
                        componentId++;
                        continue;
                    }

                    // If we have an invalid offset
                    if (list.DescriptorOffset <= 0)
                    {
                        componentId++;
                        continue;
                    }

                    // Seek to the component
                    data.Seek(descriptorOffset + list.DescriptorOffset, SeekOrigin.Begin);

                    // Add the component
                    cabinet.Components[componentId++] = ParseComponent(data, majorVersion, descriptorOffset);
                }

                #endregion

                // TODO: Parse setup types

                return cabinet;
            }
            catch
            {
                // Ignore the actual error
                return null;
            }
        }

        /// <summary>
        /// Parse a Stream into a CommonHeader
        /// </summary>
        /// <param name="data">Stream to parse</param>
        /// <returns>Filled CommonHeader on success, null on error</returns>
        public static CommonHeader ParseCommonHeader(Stream data)
        {
            var obj = new CommonHeader();

            byte[] signature = data.ReadBytes(4);
            obj.Signature = Encoding.ASCII.GetString(signature);
            obj.Version = data.ReadUInt32LittleEndian();
            obj.VolumeInfo = data.ReadUInt32LittleEndian();
            obj.DescriptorOffset = data.ReadUInt32LittleEndian();
            obj.DescriptorSize = data.ReadUInt32LittleEndian();

            return obj;
        }

        /// <summary>
        /// Parse a Stream into a Component
        /// </summary>
        /// <param name="data">Stream to parse</param>
        /// <param name="majorVersion">Major version of the cabinet</param>
        /// <param name="descriptorOffset">Offset of the cabinet descriptor</param>
        /// <returns>Filled Component on success, null on error</returns>
        public static Component ParseComponent(Stream data, int majorVersion, long descriptorOffset)
        {
            var obj = new Component();

            obj.IdentifierOffset = data.ReadUInt32LittleEndian();
            obj.DescriptorOffset = data.ReadUInt32LittleEndian();
            obj.DisplayNameOffset = data.ReadUInt32LittleEndian();
            obj.Status = (ComponentStatus)data.ReadUInt16LittleEndian();
            obj.PasswordOffset = data.ReadUInt32LittleEndian();
            obj.MiscOffset = data.ReadUInt32LittleEndian();
            obj.ComponentIndex = data.ReadUInt16LittleEndian();
            obj.NameOffset = data.ReadUInt32LittleEndian();
            obj.CDRomFolderOffset = data.ReadUInt32LittleEndian();
            obj.HTTPLocationOffset = data.ReadUInt32LittleEndian();
            obj.FTPLocationOffset = data.ReadUInt32LittleEndian();
            obj.Guid = new Guid[2];
            for (int i = 0; i < obj.Guid.Length; i++)
            {
                obj.Guid[i] = data.ReadGuid();
            }
            obj.CLSIDOffset = data.ReadUInt32LittleEndian();
            obj.Reserved2 = data.ReadBytes(28);
            obj.Reserved3 = data.ReadBytes(majorVersion <= 5 ? 2 : 1);
            obj.DependsCount = data.ReadUInt16LittleEndian();
            obj.DependsOffset = data.ReadUInt32LittleEndian();
            obj.FileGroupCount = data.ReadUInt16LittleEndian();
            obj.FileGroupNamesOffset = data.ReadUInt32LittleEndian();
            obj.X3Count = data.ReadUInt16LittleEndian();
            obj.X3Offset = data.ReadUInt32LittleEndian();
            obj.SubComponentsCount = data.ReadUInt16LittleEndian();
            obj.SubComponentsOffset = data.ReadUInt32LittleEndian();
            obj.NextComponentOffset = data.ReadUInt32LittleEndian();
            obj.OnInstallingOffset = data.ReadUInt32LittleEndian();
            obj.OnInstalledOffset = data.ReadUInt32LittleEndian();
            obj.OnUninstallingOffset = data.ReadUInt32LittleEndian();
            obj.OnUninstalledOffset = data.ReadUInt32LittleEndian();

            // Cache the current position
            long currentPosition = data.Position;

            // Read the identifier, if possible
            if (obj.IdentifierOffset != 0)
            {
                // Seek to the identifier
                data.Seek(descriptorOffset + obj.IdentifierOffset, SeekOrigin.Begin);

                // Read the string
                if (majorVersion >= 17)
                    obj.Identifier = data.ReadNullTerminatedUnicodeString();
                else
                    obj.Identifier = data.ReadNullTerminatedAnsiString();
            }

            // Read the display name, if possible
            if (obj.DisplayNameOffset != 0)
            {
                // Seek to the name
                data.Seek(descriptorOffset + obj.DisplayNameOffset, SeekOrigin.Begin);

                // Read the string
                if (majorVersion >= 17)
                    obj.DisplayName = data.ReadNullTerminatedUnicodeString();
                else
                    obj.DisplayName = data.ReadNullTerminatedAnsiString();
            }

            // Read the name, if possible
            if (obj.NameOffset != 0)
            {
                // Seek to the name
                data.Seek(descriptorOffset + obj.NameOffset, SeekOrigin.Begin);

                // Read the string
                if (majorVersion >= 17)
                    obj.Name = data.ReadNullTerminatedUnicodeString();
                else
                    obj.Name = data.ReadNullTerminatedAnsiString();
            }

            // Read the CLSID, if possible
            if (obj.CLSIDOffset != 0)
            {
                // Seek to the CLSID
                data.Seek(descriptorOffset + obj.CLSIDOffset, SeekOrigin.Begin);

                // Read the GUID
                obj.CLSID = data.ReadGuid();
            }

            // Read the file group names, if possible
            if (obj.FileGroupCount != 0 && obj.FileGroupNamesOffset != 0)
            {
                // Seek to the file group table offset
                data.Seek(descriptorOffset + obj.FileGroupNamesOffset, SeekOrigin.Begin);

                // Read the file group names table
                obj.FileGroupNames = new string[obj.FileGroupCount];
                for (int j = 0; j < obj.FileGroupCount; j++)
                {
                    // Get the name offset
                    uint nameOffset = data.ReadUInt32LittleEndian();

                    // Cache the current offset
                    long preNameOffset = data.Position;

                    // Seek to the name offset
                    data.Seek(descriptorOffset + nameOffset, SeekOrigin.Begin);

                    if (majorVersion >= 17)
                        obj.FileGroupNames[j] = data.ReadNullTerminatedUnicodeString() ?? string.Empty;
                    else
                        obj.FileGroupNames[j] = data.ReadNullTerminatedAnsiString() ?? string.Empty;

                    // Seek back to the original position
                    data.Seek(preNameOffset, SeekOrigin.Begin);
                }
            }

            // Seek back to the correct offset
            data.Seek(currentPosition, SeekOrigin.Begin);

            return obj;
        }

        /// <summary>
        /// Parse a Stream into a Descriptor
        /// </summary>
        /// <param name="data">Stream to parse</param>
        /// <returns>Filled Descriptor on success, null on error</returns>
        public static Descriptor ParseDescriptor(Stream data)
        {
            var obj = new Descriptor();

            obj.StringsOffset = data.ReadUInt32LittleEndian();
            obj.Reserved0 = data.ReadUInt32LittleEndian();
            obj.ComponentListOffset = data.ReadUInt32LittleEndian();
            obj.FileTableOffset = data.ReadUInt32LittleEndian();
            obj.Reserved1 = data.ReadUInt32LittleEndian();
            obj.FileTableSize = data.ReadUInt32LittleEndian();
            obj.FileTableSize2 = data.ReadUInt32LittleEndian();
            obj.DirectoryCount = data.ReadUInt16LittleEndian();
            obj.Reserved2 = data.ReadUInt32LittleEndian();
            obj.Reserved3 = data.ReadUInt16LittleEndian();
            obj.Reserved4 = data.ReadUInt32LittleEndian();
            obj.FileCount = data.ReadUInt32LittleEndian();
            obj.FileTableOffset2 = data.ReadUInt32LittleEndian();
            obj.ComponentTableInfoCount = data.ReadUInt16LittleEndian();
            obj.ComponentTableOffset = data.ReadUInt32LittleEndian();
            obj.Reserved5 = data.ReadUInt32LittleEndian();
            obj.Reserved6 = data.ReadUInt32LittleEndian();
            obj.FileGroupOffsets = new uint[71];
            for (int i = 0; i < 71; i++)
            {
                obj.FileGroupOffsets[i] = data.ReadUInt32LittleEndian();
            }
            obj.ComponentOffsets = new uint[71];
            for (int i = 0; i < 71; i++)
            {
                obj.ComponentOffsets[i] = data.ReadUInt32LittleEndian();
            }
            obj.SetupTypesOffset = data.ReadUInt32LittleEndian();
            obj.SetupTableOffset = data.ReadUInt32LittleEndian();
            obj.Reserved7 = data.ReadUInt32LittleEndian();
            obj.Reserved8 = data.ReadUInt32LittleEndian();

            return obj;
        }

        /// <summary>
        /// Parse a Stream into a directory name
        /// </summary>
        /// <param name="data">Stream to parse</param>
        /// <param name="majorVersion">Major version of the cabinet</param>
        /// <returns>Filled directory name on success, null on error</returns>
        public static string? ParseDirectoryName(Stream data, int majorVersion)
        {
            // Read the string
            if (majorVersion >= 17)
                return data.ReadNullTerminatedUnicodeString();
            else
                return data.ReadNullTerminatedAnsiString();
        }

        /// <summary>
        /// Parse a Stream into a FileDescriptor
        /// </summary>
        /// <param name="data">Stream to parse</param>
        /// <param name="majorVersion">Major version of the cabinet</param>
        /// <param name="descriptorOffset">Offset of the cabinet descriptor</param>
        /// <returns>Filled FileDescriptor on success, null on error</returns>
        public static FileDescriptor ParseFileDescriptor(Stream data, int majorVersion, long descriptorOffset)
        {
            var obj = new FileDescriptor();

            // Read the descriptor based on version
            if (majorVersion <= 5)
            {
                obj.Volume = 0xFFFF; // Set by the header index
                obj.NameOffset = data.ReadUInt32LittleEndian();
                obj.DirectoryIndex = data.ReadUInt32LittleEndian();
                obj.Flags = (FileFlags)data.ReadUInt16LittleEndian();
                obj.ExpandedSize = data.ReadUInt32LittleEndian();
                obj.CompressedSize = data.ReadUInt32LittleEndian();
                _ = data.ReadBytes(0x14); // Skip 0x14 bytes, unknown data?
                obj.DataOffset = data.ReadUInt32LittleEndian();

                if (majorVersion == 5)
                    obj.MD5 = data.ReadBytes(0x10);
            }
            else
            {
                obj.Flags = (FileFlags)data.ReadUInt16LittleEndian();
                obj.ExpandedSize = data.ReadUInt64LittleEndian();
                obj.CompressedSize = data.ReadUInt64LittleEndian();
                obj.DataOffset = data.ReadUInt64LittleEndian();
                obj.MD5 = data.ReadBytes(0x10);
                _ = data.ReadBytes(0x10); // Skip 0x10 bytes, unknown data?
                obj.NameOffset = data.ReadUInt32LittleEndian();
                obj.DirectoryIndex = data.ReadUInt16LittleEndian();
                _ = data.ReadBytes(0x0C); // Skip 0x0C bytes, unknown data?
                obj.LinkPrevious = data.ReadUInt32LittleEndian();
                obj.LinkNext = data.ReadUInt32LittleEndian();
                obj.LinkFlags = (LinkFlags)data.ReadByteValue();
                obj.Volume = data.ReadUInt16LittleEndian();
            }

            // Cache the current position
            long currentPosition = data.Position;

            // Read the name, if possible
            if (obj.NameOffset != 0)
            {
                // Seek to the name
                data.Seek(descriptorOffset + obj.NameOffset, SeekOrigin.Begin);

                // Read the string
                if (majorVersion >= 17)
                    obj.Name = data.ReadNullTerminatedUnicodeString();
                else
                    obj.Name = data.ReadNullTerminatedAnsiString();
            }

            // Seek back to the correct offset
            data.Seek(currentPosition, SeekOrigin.Begin);

            return obj;
        }

        /// <summary>
        /// Parse a Stream into a FileGroup
        /// </summary>
        /// <param name="data">Stream to parse</param>
        /// <param name="majorVersion">Major version of the cabinet</param>
        /// <param name="descriptorOffset">Offset of the cabinet descriptor</param>
        /// <returns>Filled FileGroup on success, null on error</returns>
        public static FileGroup ParseFileGroup(Stream data, int majorVersion, long descriptorOffset)
        {
            var obj = new FileGroup();

            obj.NameOffset = data.ReadUInt32LittleEndian();
            obj.ExpandedSize = data.ReadUInt32LittleEndian();
            obj.CompressedSize = data.ReadUInt32LittleEndian();
            obj.Attributes = (FileGroupAttributes)data.ReadUInt16LittleEndian();

            // TODO: Figure out what data lives in this area for V5 and below
            if (majorVersion <= 5)
                data.Seek(0x36, SeekOrigin.Current);

            obj.FirstFile = data.ReadUInt32LittleEndian();
            obj.LastFile = data.ReadUInt32LittleEndian();
            obj.UnknownStringOffset = data.ReadUInt32LittleEndian();
            obj.OperatingSystemOffset = data.ReadUInt32LittleEndian();
            obj.LanguageOffset = data.ReadUInt32LittleEndian();
            obj.HTTPLocationOffset = data.ReadUInt32LittleEndian();
            obj.FTPLocationOffset = data.ReadUInt32LittleEndian();
            obj.MiscOffset = data.ReadUInt32LittleEndian();
            obj.TargetDirectoryOffset = data.ReadUInt32LittleEndian();
            obj.OverwriteFlags = (FileGroupFlags)data.ReadUInt32LittleEndian();
            obj.Reserved = new uint[4];
            for (int i = 0; i < obj.Reserved.Length; i++)
            {
                obj.Reserved[i] = data.ReadUInt32LittleEndian();
            }

            // Cache the current position
            long currentPosition = data.Position;

            // Read the name, if possible
            if (obj.NameOffset != 0)
            {
                // Seek to the name
                data.Seek(descriptorOffset + obj.NameOffset, SeekOrigin.Begin);

                // Read the string
                if (majorVersion >= 17)
                    obj.Name = data.ReadNullTerminatedUnicodeString();
                else
                    obj.Name = data.ReadNullTerminatedAnsiString();
            }

            // Seek back to the correct offset
            data.Seek(currentPosition, SeekOrigin.Begin);

            return obj;
        }

        /// <summary>
        /// Parse a Stream into an OffsetList
        /// </summary>
        /// <param name="data">Stream to parse</param>
        /// <param name="majorVersion">Major version of the cabinet</param>
        /// <param name="descriptorOffset">Offset of the cabinet descriptor</param>
        /// <returns>Filled OffsetList on success, null on error</returns>
        public static OffsetList ParseOffsetList(Stream data, int majorVersion, long descriptorOffset)
        {
            var obj = new OffsetList();

            obj.NameOffset = data.ReadUInt32LittleEndian();
            obj.DescriptorOffset = data.ReadUInt32LittleEndian();
            obj.NextOffset = data.ReadUInt32LittleEndian();

            // Cache the current offset
            long currentOffset = data.Position;

            // Seek to the name offset
            data.Seek(descriptorOffset + obj.NameOffset, SeekOrigin.Begin);

            // Read the string
            if (majorVersion >= 17)
                obj.Name = data.ReadNullTerminatedUnicodeString();
            else
                obj.Name = data.ReadNullTerminatedAnsiString();

            // Seek back to the correct offset
            data.Seek(currentOffset, SeekOrigin.Begin);

            return obj;
        }

        /// <summary>
        /// Parse a Stream into a VolumeHeader
        /// </summary>
        /// <param name="data">Stream to parse</param>
        /// <param name="majorVersion">Major version of the cabinet</param>
        /// <returns>Filled VolumeHeader on success, null on error</returns>
        public static VolumeHeader ParseVolumeHeader(Stream data, int majorVersion)
        {
            var obj = new VolumeHeader();

            // Read the descriptor based on version
            if (majorVersion <= 5)
            {
                obj.DataOffset = data.ReadUInt32LittleEndian();
                _ = data.ReadBytes(0x04); // Skip 0x04 bytes, unknown data?
                obj.FirstFileIndex = data.ReadUInt32LittleEndian();
                obj.LastFileIndex = data.ReadUInt32LittleEndian();
                obj.FirstFileOffset = data.ReadUInt32LittleEndian();
                obj.FirstFileSizeExpanded = data.ReadUInt32LittleEndian();
                obj.FirstFileSizeCompressed = data.ReadUInt32LittleEndian();
                obj.LastFileOffset = data.ReadUInt32LittleEndian();
                obj.LastFileSizeExpanded = data.ReadUInt32LittleEndian();
                obj.LastFileSizeCompressed = data.ReadUInt32LittleEndian();
            }
            else
            {
                obj.DataOffset = data.ReadUInt32LittleEndian();
                obj.DataOffsetHigh = data.ReadUInt32LittleEndian();
                obj.FirstFileIndex = data.ReadUInt32LittleEndian();
                obj.LastFileIndex = data.ReadUInt32LittleEndian();
                obj.FirstFileOffset = data.ReadUInt32LittleEndian();
                obj.FirstFileOffsetHigh = data.ReadUInt32LittleEndian();
                obj.FirstFileSizeExpanded = data.ReadUInt32LittleEndian();
                obj.FirstFileSizeExpandedHigh = data.ReadUInt32LittleEndian();
                obj.FirstFileSizeCompressed = data.ReadUInt32LittleEndian();
                obj.FirstFileSizeCompressedHigh = data.ReadUInt32LittleEndian();
                obj.LastFileOffset = data.ReadUInt32LittleEndian();
                obj.LastFileOffsetHigh = data.ReadUInt32LittleEndian();
                obj.LastFileSizeExpanded = data.ReadUInt32LittleEndian();
                obj.LastFileSizeExpandedHigh = data.ReadUInt32LittleEndian();
                obj.LastFileSizeCompressed = data.ReadUInt32LittleEndian();
                obj.LastFileSizeCompressedHigh = data.ReadUInt32LittleEndian();
            }

            return obj;
        }
    }
}
