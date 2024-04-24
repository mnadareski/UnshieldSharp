using System.IO;
using System.Runtime.InteropServices;
using SabreTools.IO.Extensions;
using IA3 = SabreTools.Models.InstallShieldArchiveV3;

namespace UnshieldSharp.Archive
{
    /// <summary>
    /// A single directory in an InstallShield archive
    /// </summary>
    public class ArchiveDirectory
    {
        /// <summary>
        /// Internal directory name
        /// </summary>
        public string? Name => _directory.Name;

        /// <summary>
        /// Chunk size
        /// </summary>
        public ushort ChunkSize => _directory.ChunkSize;

        /// <summary>
        /// Total number of files in the directory
        /// </summary>
        public ushort FileCount => _directory.FileCount;

        /// <summary>
        /// Internal representation of the archive directory
        /// </summary>
        private readonly IA3.Directory _directory;

        private ArchiveDirectory(IA3.Directory directory)
        {
            _directory = directory;
        }

        /// <summary>
        /// Populate a compressed file from an input Stream
        /// </summary>
        public static ArchiveDirectory? Create(Stream stream)
        {
            if (!stream.CanRead || stream.Position >= stream.Length)
                return null;

            var directory = stream.ReadType<IA3.Directory>();
            if (directory == null)
                return null;

            return new ArchiveDirectory(directory);
        }
    }
}