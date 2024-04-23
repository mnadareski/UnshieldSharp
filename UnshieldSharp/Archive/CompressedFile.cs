using System.IO;
using SabreTools.IO.Extensions;
using SabreTools.Models.InstallShieldArchiveV3;

namespace UnshieldSharp.Archive
{
    /// <summary>
    /// A single compressed file in an InstallShield archive
    /// </summary>
    public class CompressedFile
    {
        /// <summary>
        /// Filename of the compressed file
        /// </summary>
        public string? Name => _file.Name;

        /// <summary>
        /// Full internal path of the compressed file
        /// </summary>
        public string? FullPath { get; set; }

        /// <summary>
        /// Size of the compressed file in bytes
        /// </summary>
        public uint CompressedSize => _file.CompressedSize;

        /// <summary>
        /// Offset of the file within the parent archive
        /// </summary>
        public uint Offset => _file.Offset;

        /// <summary>
        /// Size of the chunk
        /// </summary>
        public uint ChunkSize => _file.ChunkSize;

        /// <summary>
        /// Internal representation of the compressed file
        /// </summary>
        private readonly SabreTools.Models.InstallShieldArchiveV3.File _file;

        private CompressedFile(SabreTools.Models.InstallShieldArchiveV3.File file)
        {
            _file = file;
        }

        /// <summary>
        /// Populate a compressed file from an input Stream
        /// </summary>
        public static CompressedFile? Create(Stream stream)
        {
            if (!stream.CanRead || stream.Length - stream.Position < 51)
                return null;

            var file = new SabreTools.Models.InstallShieldArchiveV3.File();

            file.VolumeEnd          = stream.ReadByteValue();              // 00
            file.Index              = stream.ReadUInt16();                 // 01-02
            file.UncompressedSize   = stream.ReadUInt32();                 // 03-06
            file.CompressedSize     = stream.ReadUInt32();                 // 07-0A
            file.Offset             = stream.ReadUInt32();                 // 0B-0E
            file.DateTime           = stream.ReadUInt32();                 // 0F-12
            file.Reserved0          = stream.ReadUInt32();                 // 13-16
            file.ChunkSize          = stream.ReadUInt16();                 // 17-18
            file.Attrib             = (Attributes)stream.ReadByteValue();  // 19
            file.IsSplit            = stream.ReadByteValue();              // 1A
            file.Reserved1          = stream.ReadByteValue();              // 1B
            file.VolumeStart        = stream.ReadByteValue();              // 1C
            file.Name               = stream.ReadPrefixedAnsiString()!;    // 1D-XX

            return new CompressedFile(file);
        }
    }
}