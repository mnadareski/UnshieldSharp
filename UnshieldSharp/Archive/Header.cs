using System.IO;
using SabreTools.IO.Extensions;

namespace UnshieldSharp.Archive
{
    /// <summary>
    /// Header for an InstallShield archive
    /// </summary>
    public class Header
    {
        /// <summary>
        /// 4-byte signature
        /// </summary>
        public uint Signature => _header.Signature1;

        /// <summary>
        /// Total number of files in the archive
        /// </summary>
        public ushort FileCount => _header.FileCount;

        /// <summary>
        /// Total size of the archived data
        /// </summary>
        public uint ArchiveSize => _header.CompressedSize;

        /// <summary>
        /// Address of the table of contents
        /// </summary>
        public uint TocAddress => _header.TocAddress;

        /// <summary>
        /// Total number of directories in the archive
        /// </summary>
        public ushort DirCount => _header.DirCount;

        /// <summary>
        /// Internal representation of the header
        /// </summary>
        private readonly SabreTools.Models.InstallShieldArchiveV3.Header _header;

        private Header(SabreTools.Models.InstallShieldArchiveV3.Header header)
        {
            _header = header;
        }

        /// <summary>
        /// Populate a header from an input Stream
        /// </summary>
        public static Header? Create(Stream stream)
        {
            if (!stream.CanRead || stream.Length - stream.Position < 51)
                return null;

            var header = new SabreTools.Models.InstallShieldArchiveV3.Header();

            header.Signature1           = stream.ReadUInt32();      // 00-03
            header.Signature2           = stream.ReadUInt32();      // 04-07
            header.Reserved0            = stream.ReadUInt16();      // 08-09
            header.IsMultivolume        = stream.ReadUInt16();      // 0A-0B
            header.FileCount            = stream.ReadUInt16();      // 0C-0D
            header.DateTime             = stream.ReadUInt32();      // OE-11
            header.CompressedSize       = stream.ReadUInt32();      // 12-15
            header.UncompressedSize     = stream.ReadUInt32();      // 16-19
            header.Reserved1            = stream.ReadUInt32();      // 1A-1D
            header.VolumeTotal          = stream.ReadByteValue();   // 1E
            header.VolumeNumber         = stream.ReadByteValue();   // 1F
            header.Reserved2            = stream.ReadByteValue();   // 20
            header.SplitBeginAddress    = stream.ReadUInt32();      // 21-24
            header.SplitEndAddress      = stream.ReadUInt32();      // 25-28
            header.TocAddress           = stream.ReadUInt32();      // 29-2C
            header.Reserved3            = stream.ReadUInt32();      // 2D-30
            header.DirCount             = stream.ReadUInt16();      // 31-32
            header.Reserved4            = stream.ReadUInt32();      // 33-36
            header.Reserved5            = stream.ReadUInt32();      // 37-3A

            return new Header(header);
        }
    }
}