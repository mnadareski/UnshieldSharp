using System;
using System.IO;

namespace UnshieldSharp
{
    public class UnshieldReader
    {
        /// <summary>
        /// Cabinet file to read from
        /// </summary>
        public UnshieldCabinet Cabinet { get; private set; }

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
        public static UnshieldReader Create(UnshieldCabinet cabinet, int index, FileDescriptor fileDescriptor)
        {
            var reader = new UnshieldReader
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
                    // unshield_trace("Trying next volume...");
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
            // unshield_trace("Open volume %i", volume);

            this.VolumeFile?.Close();
            this.VolumeFile = this.Cabinet.OpenFileForReading(volume, Constants.CABINET_SUFFIX);
            if (this.VolumeFile == null)
            {
                Console.Error.WriteLine("Failed to open input cabinet file %i", volume);
                return false;
            }

            var commonHeader = CommonHeader.Create(this.VolumeFile);
            if (commonHeader == default)
                return false;

            this.VolumeHeader = VolumeHeader.Create(this.VolumeFile, this.Cabinet.HeaderList.MajorVersion);
            if (this.VolumeHeader == null)
                return false;
            
            /*
            unshield_trace("First file index = %i, last file index = %i",
                reader->volume_header.first_file_index, reader->volume_header.last_file_index);
            unshield_trace("First file offset = %08x, last file offset = %08x",
                reader->volume_header.first_file_offset, reader->volume_header.last_file_offset);
            */

            // enable support for split archives for IS5
            if (this.Cabinet.HeaderList.MajorVersion == 5)
            {
                if (this.Index < (this.Cabinet.HeaderList.CabDescriptor.FileCount - 1)
                    && this.Index == this.VolumeHeader.LastFileIndex
                    && this.VolumeHeader.LastFileSizeCompressed != this.FileDescriptor.CompressedSize)
                {
                    // unshield_trace("IS5 split file last in volume");
                    this.FileDescriptor.Flags |= FileDescriptorFlag.FILE_SPLIT;
                }
                else if (this.Index > 0
                    && this.Index == this.VolumeHeader.FirstFileIndex
                    && this.VolumeHeader.FirstFileSizeCompressed != this.FileDescriptor.CompressedSize)
                {
                    // unshield_trace("IS5 split file first in volume");
                    this.FileDescriptor.Flags |= FileDescriptorFlag.FILE_SPLIT;
                }
            }

            ulong dataOffset, volumeBytesLeftCompressed, volumeBytesLeftExpanded;
            if (this.FileDescriptor.Flags.HasFlag(FileDescriptorFlag.FILE_SPLIT))
            {
                // unshield_trace(/*"Total bytes left = 0x08%x, "*/"previous data offset = 0x08%x", /*total_bytes_left, */ data_offset);

                if (this.Index == this.VolumeHeader.LastFileIndex && this.VolumeHeader.LastFileOffset != 0x7FFFFFFF)
                {
                    // can be first file too
                    // unshield_trace("Index %i is last file in cabinet file %i", reader->index, volume);

                    dataOffset = this.VolumeHeader.LastFileOffset;
                    volumeBytesLeftExpanded = this.VolumeHeader.LastFileSizeExpanded;
                    volumeBytesLeftCompressed = this.VolumeHeader.LastFileSizeCompressed;
                }
                else if (this.Index == this.VolumeHeader.FirstFileIndex)
                {
                    // unshield_trace("Index %i is first file in cabinet file %i", reader->index, volume);

                    dataOffset = this.VolumeHeader.FirstFileOffset;
                    volumeBytesLeftExpanded = this.VolumeHeader.FirstFileSizeExpanded;
                    volumeBytesLeftCompressed = this.VolumeHeader.FirstFileSizeCompressed;
                }
                else
                {
                    return true;
                }

                // unshield_trace("Will read 0x%08x bytes from offset 0x%08x", volume_bytes_left_compressed, data_offset);
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
        public void Deobfuscate(ref byte[] buffer, long size)
        {
            this.Deobfuscate(ref buffer, size, this.ObfuscationOffset);
        }

        /// <summary>
        /// Deobfuscate a buffer
        /// </summary>
        public void Obfuscate(ref byte[] buffer, long size)
        {
            this.ObfuscationOffset = this.Obfuscate(ref buffer, size, this.ObfuscationOffset);
        }

        /// <summary>
        /// Read a certain number of bytes from the current volume
        /// </summary>
        public bool Read(byte[] buffer, int start, long size)
        {
            long bytesLeft = size;

            for (;;)
            {
                // Read as much as possible from this volume
                int bytesToRead = (int)Math.Min(bytesLeft, (long)this.VolumeBytesLeft);

                if (bytesToRead == 0)
                    return false;

                if (bytesToRead != this.VolumeFile.Read(buffer, start, bytesToRead))
                    return false;

                bytesLeft -= bytesToRead;
                this.VolumeBytesLeft -= (uint)bytesToRead;

                if (bytesLeft == 0)
                    break;

                // Open next volume
                if (!this.OpenVolume(this.Volume + 1))
                    return false;
            }

            if (this.FileDescriptor.Flags.HasFlag(FileDescriptorFlag.FILE_OBFUSCATED))
                this.Deobfuscate(ref buffer, size);

            return true;
        }

        /// <summary>
        /// Deobfuscate a buffer with a seed value
        /// </summary>
        /// <remarks>Seed is 0 at file start</remarks>
        private uint Deobfuscate(ref byte[] buffer, long size, uint seed)
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
        private uint Obfuscate(ref byte[] buffer, long size, uint seed)
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
