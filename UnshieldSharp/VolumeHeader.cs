using System;
using System.IO;

namespace UnshieldSharp
{
    public class VolumeHeader
    {
        public uint DataOffset { get; private set; }
        public uint DataOffsetHigh { get; private set; }
        public uint FirstFileIndex { get; private set; }
        public uint LastFileIndex { get; private set; }
        public uint FirstFileOffset { get; private set; }
        public uint FirstFileOffsetHigh { get; private set; }
        public uint FirstFileSizeExpanded { get; private set; }
        public uint FirstFileSizeExpandedHigh { get; private set; }
        public uint FirstFileSizeCompressed { get; private set; }
        public uint FirstFileSizeCompressedHigh { get; private set; }
        public uint LastFileOffset { get; private set; }
        public uint LastFileOffsetHigh { get; private set; }
        public uint LastFileSizeExpanded { get; private set; }
        public uint LastFileSizeExpandedHigh { get; private set; }
        public uint LastFileSizeCompressed { get; private set; }
        public uint LastFileSizeCompressedHigh { get; private set; }

        private const int VOLUME_HEADER_SIZE_V5 = 40;
        private const int VOLUME_HEADER_SIZE_V6 = 64;

        /// <summary>
        /// Create a new VolumeHeader from a Stream and version
        /// </summary>
        public static VolumeHeader Create(Stream stream, int version)
        {
            var header = new VolumeHeader();

            if (version <= 5)
            {
                byte[] bytes = new byte[VOLUME_HEADER_SIZE_V5];
                if (VOLUME_HEADER_SIZE_V5 != stream.Read(bytes, 0, VOLUME_HEADER_SIZE_V5))
                    return null;

                int p = 0;
                header.DataOffset = BitConverter.ToUInt32(bytes, p); p += 4;
                /* unknown */ p += 4;
                header.FirstFileIndex = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileIndex = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileOffset = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileSizeExpanded = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileSizeCompressed = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileOffset = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileSizeExpanded = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileSizeCompressed = BitConverter.ToUInt32(bytes, p); p += 4;

                if (header.LastFileOffset == 0)
                    header.LastFileOffset = int.MaxValue;
            }
            else
            {
                byte[] bytes = new byte[VOLUME_HEADER_SIZE_V6];
                if (VOLUME_HEADER_SIZE_V6 != stream.Read(bytes, 0, VOLUME_HEADER_SIZE_V6))
                    return null;

                int p = 0;
                header.DataOffset = BitConverter.ToUInt32(bytes, p); p += 4;
                header.DataOffsetHigh = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileIndex = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileIndex = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileOffset = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileOffsetHigh = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileSizeExpanded = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileSizeExpandedHigh = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileSizeCompressed = BitConverter.ToUInt32(bytes, p); p += 4;
                header.FirstFileSizeCompressedHigh = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileOffset = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileOffsetHigh = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileSizeExpanded = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileSizeExpandedHigh = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileSizeCompressed = BitConverter.ToUInt32(bytes, p); p += 4;
                header.LastFileSizeCompressedHigh = BitConverter.ToUInt32(bytes, p); p += 4;
            }

            return header;
        }
    }
}
