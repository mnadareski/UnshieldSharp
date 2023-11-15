using System;
using System.IO;
using System.Text;
using SabreTools.Models.InstallShieldCabinet;
using static UnshieldSharp.Cabinet.Constants;

#pragma warning disable IDE0051 // Private member is unused

namespace UnshieldSharp.Cabinet
{
    public class Reader : IDisposable
    {
        /// <summary>
        /// Cabinet file to read from
        /// </summary>
        public InstallShieldCabinet? Cabinet { get; private set; }

        /// <summary>
        /// Current volume ID
        /// </summary>
        public int Volume { get; private set; }

        /// <summary>
        /// Handle to the current volume stream
        /// </summary>
        public Stream? VolumeFile { get; private set; }

        /// <summary>
        /// Number of bytes left in the current volume
        /// </summary>
        public ulong VolumeBytesLeft { get; private set; }

        /// <summary>
        /// Currently selected index
        /// </summary>
        private uint _index;

        /// <summary>
        /// File descriptor defining the currently selected index
        /// </summary>
        private FileDescriptor? _fileDescriptor;

        /// <summary>
        /// Current volume header
        /// </summary>
        private VolumeHeader? _volumeHeader;

        /// <summary>
        /// Offset for obfuscation seed
        /// </summary>
        private uint _obfuscationOffset;

        /// <summary>
        /// Create a new UnshieldReader from an existing cabinet, index, and file descriptor
        /// </summary>
        public static Reader? Create(InstallShieldCabinet cabinet, int index, FileDescriptor fileDescriptor)
        {
            var reader = new Reader
            {
                Cabinet = cabinet,
                _index = (uint)index,
                _fileDescriptor = fileDescriptor,
            };

            for (; ; )
            {
                if (!reader.OpenVolume(fileDescriptor.Volume))
                {
                    Console.Error.WriteLine($"Failed to open volume {fileDescriptor.Volume}");
                    return null;
                }

                // Start with the correct volume for IS5 cabinets
                if (reader.Cabinet!.HeaderList!.MajorVersion <= 5 && index > (int)reader._volumeHeader!.LastFileIndex)
                {
                    fileDescriptor.Volume++;
                    continue;
                }

                break;
            }

            return reader;
        }

        /// <summary>
        /// Dispose of the current object
        /// </summary>
        public void Dispose()
        {
            VolumeFile?.Close();
        }

        /// <summary>
        /// Open the volume at the inputted index
        /// </summary>
        public bool OpenVolume(int volume)
        {
            VolumeFile?.Close();
            VolumeFile = Cabinet!.OpenFileForReading(volume, CABINET_SUFFIX);
            if (VolumeFile == null)
            {
                Console.Error.WriteLine($"Failed to open input cabinet file {volume}");
                return false;
            }

            var commonHeader = CreateCommonHeader(VolumeFile);
            if (commonHeader == default)
                return false;

            _volumeHeader = CreateVolumeHeader(VolumeFile, Cabinet.HeaderList!.MajorVersion);
            if (_volumeHeader == null)
                return false;

            // Enable support for split archives for IS5
            if (Cabinet.HeaderList.MajorVersion == 5)
            {
                if (_index < (Cabinet.HeaderList.FileCount - 1)
                    && _index == _volumeHeader.LastFileIndex
                    && _volumeHeader.LastFileSizeCompressed != _fileDescriptor!.CompressedSize)
                {
                    _fileDescriptor.Flags |= FileFlags.FILE_SPLIT;
                }
                else if (_index > 0
                    && _index == _volumeHeader.FirstFileIndex
                    && _volumeHeader.FirstFileSizeCompressed != _fileDescriptor!.CompressedSize)
                {
                    _fileDescriptor.Flags |= FileFlags.FILE_SPLIT;
                }
            }

            ulong dataOffset, volumeBytesLeftCompressed, volumeBytesLeftExpanded;
            if (_fileDescriptor!.Flags.HasFlag(FileFlags.FILE_SPLIT))
            {
                if (_index == _volumeHeader.LastFileIndex && _volumeHeader.LastFileOffset != 0x7FFFFFFF)
                {
                    // can be first file too
                    dataOffset = _volumeHeader.LastFileOffset;
                    volumeBytesLeftExpanded = _volumeHeader.LastFileSizeExpanded;
                    volumeBytesLeftCompressed = _volumeHeader.LastFileSizeCompressed;
                }
                else if (_index == _volumeHeader.FirstFileIndex)
                {
                    dataOffset = _volumeHeader.FirstFileOffset;
                    volumeBytesLeftExpanded = _volumeHeader.FirstFileSizeExpanded;
                    volumeBytesLeftCompressed = _volumeHeader.FirstFileSizeCompressed;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                dataOffset = _fileDescriptor.DataOffset;
                volumeBytesLeftExpanded = _fileDescriptor.ExpandedSize;
                volumeBytesLeftCompressed = _fileDescriptor.CompressedSize;
            }

            if (_fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
                VolumeBytesLeft = volumeBytesLeftCompressed;
            else
                VolumeBytesLeft = volumeBytesLeftExpanded;

            VolumeFile.Seek((long)dataOffset, SeekOrigin.Begin);
            Volume = volume;

            return true;
        }

        /// <summary>
        /// Read a certain number of bytes from the current volume
        /// </summary>
        public bool Read(byte[] buffer, int start, long size)
        {
            long bytesLeft = size;
            while (bytesLeft > 0)
            {
                // Read as much as possible from this volume
                int bytesToRead = (int)Math.Min(bytesLeft, (long)VolumeBytesLeft);

                if (bytesToRead == 0)
                    return false;

                if (bytesToRead != VolumeFile!.Read(buffer, start, bytesToRead))
                    return false;

                bytesLeft -= bytesToRead;
                VolumeBytesLeft -= (uint)bytesToRead;

                if (bytesLeft > 0)
                {
                    // Open next volume
                    if (!OpenVolume(Volume + 1))
                        return false;
                }
            }

            if (_fileDescriptor!.Flags.HasFlag(FileFlags.FILE_OBFUSCATED))
                Deobfuscate(buffer, size);

            return true;
        }

        // TODO: Expose the methods used here in the library instead
        #region Copied from Serialization Library

        /// <summary>
        /// Create a common header object, if possible
        /// </summary>
        private static CommonHeader? CreateCommonHeader(Stream? data)
        {
            if (data == null)
                return null;

            var commonHeader = new CommonHeader();
            byte[] array = data.ReadBytes(4);
            if (array == null)
                return null;

            commonHeader.Signature = Encoding.ASCII.GetString(array);
            if (commonHeader.Signature != "ISc(")
                return null;

            commonHeader.Version = data.ReadUInt32();
            commonHeader.VolumeInfo = data.ReadUInt32();
            commonHeader.DescriptorOffset = data.ReadUInt32();
            commonHeader.DescriptorSize = data.ReadUInt32();

            return commonHeader;
        }

        /// <summary>
        /// Create a volume header object, if possible
        /// </summary>
        private static VolumeHeader? CreateVolumeHeader(Stream? data, int majorVersion)
        {
            if (data == null)
                return null;

            var volumeHeader = new VolumeHeader();
            if (majorVersion <= 5)
            {
                volumeHeader.DataOffset = data.ReadUInt32();
                data.ReadBytes(4);
                volumeHeader.FirstFileIndex = data.ReadUInt32();
                volumeHeader.LastFileIndex = data.ReadUInt32();
                volumeHeader.FirstFileOffset = data.ReadUInt32();
                volumeHeader.FirstFileSizeExpanded = data.ReadUInt32();
                volumeHeader.FirstFileSizeCompressed = data.ReadUInt32();
                volumeHeader.LastFileOffset = data.ReadUInt32();
                volumeHeader.LastFileSizeExpanded = data.ReadUInt32();
                volumeHeader.LastFileSizeCompressed = data.ReadUInt32();
            }
            else
            {
                volumeHeader.DataOffset = data.ReadUInt32();
                volumeHeader.DataOffsetHigh = data.ReadUInt32();
                volumeHeader.FirstFileIndex = data.ReadUInt32();
                volumeHeader.LastFileIndex = data.ReadUInt32();
                volumeHeader.FirstFileOffset = data.ReadUInt32();
                volumeHeader.FirstFileOffsetHigh = data.ReadUInt32();
                volumeHeader.FirstFileSizeExpanded = data.ReadUInt32();
                volumeHeader.FirstFileSizeExpandedHigh = data.ReadUInt32();
                volumeHeader.FirstFileSizeCompressed = data.ReadUInt32();
                volumeHeader.FirstFileSizeCompressedHigh = data.ReadUInt32();
                volumeHeader.LastFileOffset = data.ReadUInt32();
                volumeHeader.LastFileOffsetHigh = data.ReadUInt32();
                volumeHeader.LastFileSizeExpanded = data.ReadUInt32();
                volumeHeader.LastFileSizeExpandedHigh = data.ReadUInt32();
                volumeHeader.LastFileSizeCompressed = data.ReadUInt32();
                volumeHeader.LastFileSizeCompressedHigh = data.ReadUInt32();
            }

            return volumeHeader;
        }

        #endregion

        /// <summary>
        /// Deobfuscate a buffer
        /// </summary>
        private void Deobfuscate(byte[] buffer, long size)
        {
            _obfuscationOffset = Deobfuscate(buffer, size, _obfuscationOffset);
        }

        /// <summary>
        /// Deobfuscate a buffer with a seed value
        /// </summary>
        /// <remarks>Seed is 0 at file start</remarks>
        private static uint Deobfuscate(byte[] buffer, long size, uint seed)
        {
            for (int i = 0; size > 0; size--, i++, seed++)
            {
                buffer[i] = (byte)(Reader.ROR8(buffer[i] ^ 0xd5, 2) - (seed % 0x47));
            }

            return seed;
        }

        /// <summary>
        /// Deobfuscate a buffer
        /// </summary>
        private void Obfuscate(byte[] buffer, long size)
        {
            _obfuscationOffset = Obfuscate(buffer, size, _obfuscationOffset);
        }

        /// <summary>
        /// Obfuscate a buffer with a seed value
        /// </summary>
        /// <remarks>Seed is 0 at file start</remarks>
        private static uint Obfuscate(byte[] buffer, long size, uint seed)
        {
            for (int i = 0; size > 0; size--, i++, seed++)
            {
                buffer[i] = (byte)(Reader.ROL8(buffer[i] ^ 0xd5, 2) + (seed % 0x47));
            }

            return seed;
        }

        /// <summary>
        /// Rotate Right 8
        /// </summary>
        private static int ROR8(int x, int n) { return ((x) >> ((int)(n))) | ((x) << (8 - (int)(n))); }
        
        /// <summary>
        /// Rotate Left 8
        /// </summary>
        private static int ROL8(int x, int n) { return ((x) << ((int)(n))) | ((x) >> (8 - (int)(n))); }
    }
}
