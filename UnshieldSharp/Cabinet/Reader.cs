using System;
using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class Reader
    {
        /// <summary>
        /// Cabinet file to read from
        /// </summary>
        public InstallShieldCabinet Cabinet { get; private set; }

        /// <summary>
        /// Currently selected index
        /// </summary>
        public uint Index { get; private set; }

        /// <summary>
        /// File descriptor defining the currently selected index
        /// </summary>
        public FileDescriptor FileDescriptor { get; private set; }

        /// <summary>
        /// Current volume ID
        /// </summary>
        public int Volume { get; private set; }

        /// <summary>
        /// Handle to the current volume stream
        /// </summary>
        public Stream VolumeFile { get; private set; }

        /// <summary>
        /// Current volume header
        /// </summary>
        public VolumeHeader VolumeHeader { get; private set; }

        /// <summary>
        /// Number of bytes left in the current volume
        /// </summary>
        public ulong VolumeBytesLeft { get; private set; }

        /// <summary>
        /// Offset for obfuscation seed
        /// </summary>
        public uint ObfuscationOffset { get; private set; }

        /// <summary>
        /// Create a new UnshieldReader from an existing cabinet, index, and file descriptor
        /// </summary>
        public static Reader Create(InstallShieldCabinet cabinet, int index, FileDescriptor fileDescriptor)
        {
            var reader = new Reader
            {
                Cabinet = cabinet,
                Index = (uint)index,
                FileDescriptor = fileDescriptor,
            };

            for (; ; )
            {
                if (!reader.OpenVolume(fileDescriptor.Volume))
                {
                    Console.Error.WriteLine($"Failed to open volume {fileDescriptor.Volume}");
                    return null;
                }

                // Start with the correct volume for IS5 cabinets
                if (reader.Cabinet.HeaderList.MajorVersion <= 5 && index > (int)reader.VolumeHeader.LastFileIndex)
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
            this.VolumeFile?.Close();
            this.VolumeFile = this.Cabinet.OpenFileForReading(volume, Constants.CABINET_SUFFIX);
            if (this.VolumeFile == null)
            {
                Console.Error.WriteLine($"Failed to open input cabinet file {volume}");
                return false;
            }

            var commonHeader = CommonHeader.Create(this.VolumeFile);
            if (commonHeader == default)
                return false;

            this.VolumeHeader = VolumeHeader.Create(this.VolumeFile, this.Cabinet.HeaderList.MajorVersion);
            if (this.VolumeHeader == null)
                return false;

            // enable support for split archives for IS5
            if (this.Cabinet.HeaderList.MajorVersion == 5)
            {
                if (this.Index < (this.Cabinet.HeaderList.Descriptor.FileCount - 1)
                    && this.Index == this.VolumeHeader.LastFileIndex
                    && this.VolumeHeader.LastFileSizeCompressed != this.FileDescriptor.CompressedSize)
                {
                    this.FileDescriptor.Flags |= FileDescriptorFlag.FILE_SPLIT;
                }
                else if (this.Index > 0
                    && this.Index == this.VolumeHeader.FirstFileIndex
                    && this.VolumeHeader.FirstFileSizeCompressed != this.FileDescriptor.CompressedSize)
                {
                    this.FileDescriptor.Flags |= FileDescriptorFlag.FILE_SPLIT;
                }
            }

            ulong dataOffset, volumeBytesLeftCompressed, volumeBytesLeftExpanded;
            if (this.FileDescriptor.Flags.HasFlag(FileDescriptorFlag.FILE_SPLIT))
            {
                if (this.Index == this.VolumeHeader.LastFileIndex && this.VolumeHeader.LastFileOffset != 0x7FFFFFFF)
                {
                    // can be first file too
                    dataOffset = this.VolumeHeader.LastFileOffset;
                    volumeBytesLeftExpanded = this.VolumeHeader.LastFileSizeExpanded;
                    volumeBytesLeftCompressed = this.VolumeHeader.LastFileSizeCompressed;
                }
                else if (this.Index == this.VolumeHeader.FirstFileIndex)
                {
                    dataOffset = this.VolumeHeader.FirstFileOffset;
                    volumeBytesLeftExpanded = this.VolumeHeader.FirstFileSizeExpanded;
                    volumeBytesLeftCompressed = this.VolumeHeader.FirstFileSizeCompressed;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                dataOffset = this.FileDescriptor.DataOffset;
                volumeBytesLeftExpanded = this.FileDescriptor.ExpandedSize;
                volumeBytesLeftCompressed = this.FileDescriptor.CompressedSize;
            }

            if (this.FileDescriptor.Flags.HasFlag(FileDescriptorFlag.FILE_COMPRESSED))
                this.VolumeBytesLeft = volumeBytesLeftCompressed;
            else
                this.VolumeBytesLeft = volumeBytesLeftExpanded;

            this.VolumeFile.Seek((long)dataOffset, SeekOrigin.Begin);
            this.Volume = volume;

            return true;
        }

        /// <summary>
        /// Deobfuscate a buffer
        /// </summary>
        public void Deobfuscate(byte[] buffer, long size)
        {
            this.ObfuscationOffset = this.Deobfuscate(buffer, size, this.ObfuscationOffset);
        }

        /// <summary>
        /// Deobfuscate a buffer
        /// </summary>
        public void Obfuscate(byte[] buffer, long size)
        {
            this.ObfuscationOffset = this.Obfuscate(buffer, size, this.ObfuscationOffset);
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
                int bytesToRead = (int)Math.Min(bytesLeft, (long)this.VolumeBytesLeft);

                if (bytesToRead == 0)
                    return false;

                if (bytesToRead != this.VolumeFile.Read(buffer, start, bytesToRead))
                    return false;

                bytesLeft -= bytesToRead;
                this.VolumeBytesLeft -= (uint)bytesToRead;

                if (bytesLeft > 0)
                {
                    // Open next volume
                    if (!this.OpenVolume(this.Volume + 1))
                        return false;
                }
            }

            if (this.FileDescriptor.Flags.HasFlag(FileDescriptorFlag.FILE_OBFUSCATED))
                this.Deobfuscate(buffer, size);

            return true;
        }

        /// <summary>
        /// Deobfuscate a buffer with a seed value
        /// </summary>
        /// <remarks>Seed is 0 at file start</remarks>
        private uint Deobfuscate(byte[] buffer, long size, uint seed)
        {
            for (int i = 0; size > 0; size--, i++, seed++)
            {
                buffer[i] = (byte)(ROR8(buffer[i] ^ 0xd5, 2) - (seed % 0x47));
            }

            return seed;
        }

        /// <summary>
        /// Obfuscate a buffer with a seed value
        /// </summary>
        /// <remarks>Seed is 0 at file start</remarks>
        private uint Obfuscate(byte[] buffer, long size, uint seed)
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
        private int ROR8(int x, int n) { return ((x) >> ((int)(n))) | ((x) << (8 - (int)(n))); }
        
        /// <summary>
        /// Rotate Left 8
        /// </summary>
        private int ROL8(int x, int n) { return ((x) << ((int)(n))) | ((x) >> (8 - (int)(n))); }
    }
}
