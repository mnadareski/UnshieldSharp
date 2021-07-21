using System.IO;

namespace UnshieldSharp.Cabinet
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

        /// <summary>
        /// Create a new VolumeHeader from a Stream and version
        /// </summary>
        public static VolumeHeader Create(Stream stream, int version)
        {
            var header = new VolumeHeader();

            if (version <= 5)
            {
                if (stream.Length - stream.Position < Constants.VOLUME_HEADER_SIZE_V5)
                    return null;

                header.DataOffset = stream.ReadUInt32();
                stream.Seek(4, SeekOrigin.Current);
                header.FirstFileIndex = stream.ReadUInt32();
                header.LastFileIndex = stream.ReadUInt32();
                header.FirstFileOffset = stream.ReadUInt32();
                header.FirstFileSizeExpanded = stream.ReadUInt32();
                header.FirstFileSizeCompressed = stream.ReadUInt32();
                header.LastFileOffset = stream.ReadUInt32();
                header.LastFileSizeExpanded = stream.ReadUInt32();
                header.LastFileSizeCompressed = stream.ReadUInt32();

                if (header.LastFileOffset == 0)
                    header.LastFileOffset = int.MaxValue;
            }
            else
            {
                if (stream.Length - stream.Position < Constants.VOLUME_HEADER_SIZE_V6)
                    return null;

                header.DataOffset = stream.ReadUInt32();
                header.DataOffsetHigh = stream.ReadUInt32();
                header.FirstFileIndex = stream.ReadUInt32();
                header.LastFileIndex = stream.ReadUInt32();
                header.FirstFileOffset = stream.ReadUInt32();
                header.FirstFileOffsetHigh = stream.ReadUInt32();
                header.FirstFileSizeExpanded = stream.ReadUInt32();
                header.FirstFileSizeExpandedHigh = stream.ReadUInt32();
                header.FirstFileSizeCompressed = stream.ReadUInt32();
                header.FirstFileSizeCompressedHigh = stream.ReadUInt32();
                header.LastFileOffset = stream.ReadUInt32();
                header.LastFileOffsetHigh = stream.ReadUInt32();
                header.LastFileSizeExpanded = stream.ReadUInt32();
                header.LastFileSizeExpandedHigh = stream.ReadUInt32();
                header.LastFileSizeCompressed = stream.ReadUInt32();
                header.LastFileSizeCompressedHigh = stream.ReadUInt32();
            }

            return header;
        }
    }
}
