using System.IO;

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
        public uint Signature { get; set; }
        
        /// <summary>
        /// UNKONWN INFORMATION
        /// </summary>
        public byte[] Ignore0 { get; set; } = new byte[8];

        /// <summary>
        /// Total number of files in the archive
        /// </summary>
        public ushort FileCount { get; set; }

        /// <summary>
        /// UNKONWN INFORMATION
        /// </summary>
        public byte[] Ignore1 { get; set; } = new byte[4];

        /// <summary>
        /// Total size of the archived data
        /// </summary>
        public uint ArchiveSize { get; set; }

        /// <summary>
        /// UNKONWN INFORMATION
        /// </summary>
        public byte[] Ignore2 { get; set; } = new byte[19];

        /// <summary>
        /// Address of the table of contents
        /// </summary>
        public uint TocAddress { get; set; }

        /// <summary>
        /// UNKONWN INFORMATION
        /// </summary>
        public byte[] Ignore3 { get; set; } = new byte[4];

        /// <summary>
        /// Total number of directories in the archive
        /// </summary>
        public ushort DirCount { get; set; }

        /// <summary>
        /// Populate a header from an input Stream
        /// </summary>
        public static Header? Create(Stream stream)
        {
            if (!stream.CanRead || stream.Length - stream.Position < 51)
                return null;

            var header = new Header();

            header.Signature   = stream.ReadUInt32();   // 00-03
            header.Ignore0     = stream.ReadBytes(8);   // 04-11
            header.FileCount   = stream.ReadUInt16();   // 12-13
            header.Ignore1     = stream.ReadBytes(4);   // 14-17
            header.ArchiveSize = stream.ReadUInt32();   // 18-21
            header.Ignore2     = stream.ReadBytes(19);  // 22-40
            header.TocAddress  = stream.ReadUInt32();   // 41-44
            header.Ignore3     = stream.ReadBytes(4);   // 45-48
            header.DirCount    = stream.ReadUInt16();   // 49-50

            return header;
        }
    }
}