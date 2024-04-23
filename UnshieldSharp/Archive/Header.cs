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

            var header = stream.ReadType<SabreTools.Models.InstallShieldArchiveV3.Header>();
            if (header == null)
                return null;

            return new Header(header);
        }
    }
}