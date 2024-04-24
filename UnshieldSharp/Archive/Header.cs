using System.IO;
using SabreTools.IO.Extensions;
using IA3 = SabreTools.Models.InstallShieldArchiveV3;

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
        private readonly IA3.Header _header;

        private Header(IA3.Header header)
        {
            _header = header;
        }

        /// <summary>
        /// Populate a header from an input Stream
        /// </summary>
        public static Header? Create(Stream stream)
        {
            if (!stream.CanRead || stream.Position >= stream.Length)
                return null;

            var header = stream.ReadType<IA3.Header>();
            if (header == null)
                return null;

            return new Header(header);
        }
    }
}