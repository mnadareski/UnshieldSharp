using System;
using System.IO;
using SabreTools.IO.Extensions;
using SabreTools.Models.InstallShieldCabinet;
using static SabreTools.Models.InstallShieldCabinet.Constants;

namespace UnshieldSharp
{
    internal class Reader : IDisposable
    {
        #region Private Instance Variables

        /// <summary>
        /// Cabinet file to read from
        /// </summary>
        private InstallShieldCabinet? _cabinet;

        /// <summary>
        /// Currently selected index
        /// </summary>
        private uint _index;

        /// <summary>
        /// File descriptor defining the currently selected index
        /// </summary>
        private FileDescriptor? _fileDescriptor;

        /// <summary>
        /// Number of bytes left in the current volume
        /// </summary>
        private ulong _volumeBytesLeft;

        /// <summary>
        /// Handle to the current volume stream
        /// </summary>
        private Stream? _volumeFile;

        /// <summary>
        /// Current volume header
        /// </summary>
        private VolumeHeader? _volumeHeader;

        /// <summary>
        /// Current volume ID
        /// </summary>
        private ushort _volumeId;

        /// <summary>
        /// Offset for obfuscation seed
        /// </summary>
        private uint _obfuscationOffset;

        #endregion

        /// <summary>
        /// Create a new <see cref="Reader"> from an existing cabinet, index, and file descriptor
        /// </summary>
        public static Reader? Create(InstallShieldCabinet cabinet, int index, FileDescriptor fileDescriptor)
        {
            var reader = new Reader
            {
                _cabinet = cabinet,
                _index = (uint)index,
                _fileDescriptor = fileDescriptor,
            };

            // If the cabinet header list is invalid
            if (reader._cabinet.HeaderList == null)
            {
                Console.Error.WriteLine($"Header list is invalid");
                return null;
            }

            for (; ; )
            {
                // If the volume is invalid
                if (!reader.OpenVolume(fileDescriptor.Volume))
                {
                    Console.Error.WriteLine($"Failed to open volume {fileDescriptor.Volume}");
                    return null;
                }
                else if (reader._volumeFile == null || reader._volumeHeader == null)
                {
                    Console.Error.WriteLine($"Volume {fileDescriptor.Volume} is invalid");
                    return null;
                }

                // Start with the correct volume for IS5 cabinets
                if (reader._cabinet.HeaderList.MajorVersion <= 5 && index > (int)reader._volumeHeader.LastFileIndex)
                {
                    // Normalize the volume ID for odd cases
                    if (fileDescriptor.Volume == ushort.MinValue || fileDescriptor.Volume == ushort.MaxValue)
                        fileDescriptor.Volume = 1;

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
            _volumeFile?.Close();
        }

        #region Reading

        /// <summary>
        /// Open the next volume based on the current index
        /// </summary>
        public bool OpenNextVolume(out ushort nextVolume)
        {
            nextVolume = (ushort)(_volumeId + 1);
            return OpenVolume(nextVolume);
        }

        /// <summary>
        /// Read a certain number of bytes from the current volume
        /// </summary>
        public bool Read(byte[] buffer, int start, long size)
        {
            long bytesLeft = size;
            while (bytesLeft > 0)
            {
                // Open the next volume, if necessary
                if (_volumeBytesLeft == 0)
                {
                    if (!OpenNextVolume(out _))
                        return false;
                }

                // Get the number of bytes to read from this volume
                int bytesToRead = (int)Math.Min(bytesLeft, (long)_volumeBytesLeft);
                if (bytesToRead == 0)
                    break;

                // Read as much as possible from this volume
                if (bytesToRead != _volumeFile!.Read(buffer, start, bytesToRead))
                    return false;

                // Set the number of bytes left
                bytesLeft -= bytesToRead;
                _volumeBytesLeft -= (uint)bytesToRead;
            }

#if NET20 || NET35
            if ((_fileDescriptor!.Flags & FileFlags.FILE_OBFUSCATED) != 0)
#else
            if (_fileDescriptor!.Flags.HasFlag(FileFlags.FILE_OBFUSCATED))
#endif
                Deobfuscate(buffer, size);

            return true;
        }

        /// <summary>
        /// Open the volume at the inputted index
        /// </summary>
        private bool OpenVolume(ushort volume)
        {
            // Normalize the volume ID for odd cases
            if (volume == ushort.MinValue || volume == ushort.MaxValue)
                volume = 1;

            _volumeFile?.Close();
            _volumeFile = _cabinet!.OpenFileForReading(volume, CABINET_SUFFIX);
            if (_volumeFile == null)
            {
                Console.Error.WriteLine($"Failed to open input cabinet file {volume}");
                return false;
            }

            var commonHeader = _volumeFile.ReadType<CommonHeader>();
            if (commonHeader == default)
                return false;

            _volumeHeader = SabreTools.Serialization.Deserializers.InstallShieldCabinet.ParseVolumeHeader(_volumeFile, _cabinet.HeaderList!.MajorVersion);
            if (_volumeHeader == null)
                return false;

            // Enable support for split archives for IS5
            if (_cabinet.HeaderList.MajorVersion == 5)
            {
                if (_index < (_cabinet.HeaderList.FileCount - 1)
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
#if NET20 || NET35
            if ((_fileDescriptor!.Flags & FileFlags.FILE_SPLIT) != 0)
#else
            if (_fileDescriptor!.Flags.HasFlag(FileFlags.FILE_SPLIT))
#endif
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

#if NET20 || NET35
            if ((_fileDescriptor.Flags & FileFlags.FILE_COMPRESSED) != 0)
#else
            if (_fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
#endif
                _volumeBytesLeft = volumeBytesLeftCompressed;
            else
                _volumeBytesLeft = volumeBytesLeftExpanded;

            _volumeFile.Seek((long)dataOffset, SeekOrigin.Begin);
            _volumeId = volume;

            return true;
        }

        #endregion

        #region Obfuscation

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
                buffer[i] = (byte)(ROR8(buffer[i] ^ 0xd5, 2) - (seed % 0x47));
            }

            return seed;
        }

        /// <summary>
        /// Obfuscate a buffer
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
                buffer[i] = (byte)(ROL8(buffer[i] ^ 0xd5, 2) + (seed % 0x47));
            }

            return seed;
        }

        /// <summary>
        /// Rotate Right 8
        /// </summary>
        private static int ROR8(int x, byte n) => (x >> n) | (x << (8 - n));

        /// <summary>
        /// Rotate Left 8
        /// </summary>
        private static int ROL8(int x, byte n) => (x << n) | (x >> (8 - n));

        #endregion
    }
}
