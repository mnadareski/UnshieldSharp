using System.IO;
using SabreTools.IO.Extensions;
using IA3 = SabreTools.Models.InstallShieldArchiveV3;

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
        private readonly IA3.File _file;

        private CompressedFile(IA3.File file)
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

            var file = stream.ReadType<IA3.File>();
            if (file == null)
                return null;

            return new CompressedFile(file);
        }
    }
}