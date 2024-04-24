using System.IO;
using SabreTools.Models.InstallShieldCabinet;

namespace UnshieldSharp.Cabinet
{
    public class Header
    {
        #region Fields

        /// <summary>
        /// Reference to the next cabinet header
        /// </summary>
        public Header? Next { get; set; }

        #endregion

        #region Passthrough Properties

        /// <summary>
        /// Number of components in the cabinet set
        /// </summary>
        public int ComponentCount => _cabinet.Model.Components!.Length;

        /// <summary>
        /// Number of directories in the cabinet set
        /// </summary>
        public ushort DirectoryCount => _cabinet.Model.Descriptor!.DirectoryCount;

        /// <summary>
        /// Number of files in the cabinet set
        /// </summary>
        public uint FileCount => _cabinet.Model.Descriptor!.FileCount;

        /// <summary>
        /// Number of file groups in the cabinet set
        /// </summary>
        public int FileGroupCount => _cabinet.Model.FileGroups!.Length;

        /// <summary>
        /// Internal major version of the cabinet set
        /// </summary>
        public int MajorVersion => _cabinet.MajorVersion;

        #endregion

        /// <summary>
        /// Private cabinet backing the rest of the fields
        /// </summary>
        private readonly SabreTools.Serialization.Wrappers.InstallShieldCabinet _cabinet;

        private Header(SabreTools.Serialization.Wrappers.InstallShieldCabinet cabinet)
        {
            _cabinet = cabinet;
        }

        /// <summary>
        /// Create a new Header from a stream and an index
        /// </summary>
        public static Header? Create(Stream stream, int index)
        {
            stream.Seek(index, SeekOrigin.Begin);
            var cabinet = SabreTools.Serialization.Wrappers.InstallShieldCabinet.Create(stream);
            if (cabinet == null)
                return null;

            return new Header(cabinet);
        }

        /// <summary>
        /// Returns if the file at a given index is marked as valid
        /// </summary>
        public bool FileIsValid(int index) => _cabinet.FileIsValid(index);

        /// <summary>
        /// Get the component name at a given index, if possible
        /// </summary>
        public string? GetComponentName(int index) => _cabinet.GetComponentName(index);

        /// <summary>
        /// Get the directory name at a given index, if possible
        /// </summary>
        public string? GetDirectoryName(int index) => _cabinet.GetDirectoryName(index);

        /// <summary>
        /// Get the reported expanded file size for a given index
        /// </summary>
        public ulong GetExpandedFileSize(int index) => _cabinet.GetExpandedFileSize(index);

        /// <summary>
        /// Get the file descriptor at a given index, if possible
        /// </summary>
        public FileDescriptor? GetFileDescriptor(int index) => _cabinet.GetFileDescriptor(index);

        /// <summary>
        /// Get the directory index for the given file index
        /// </summary>
        public uint GetFileDirectoryIndex(int index) => _cabinet.GetFileDirectoryIndex(index);

        /// <summary>
        /// Get the file group at a given index, if possible
        /// </summary>
        public FileGroup? GetFileGroup(int index) => _cabinet.GetFileGroup(index);

        /// <summary>
        /// Get the file group at a given name, if possible
        /// </summary>
        public FileGroup? GetFileGroup(string name) => _cabinet.GetFileGroup(name);

        /// <summary>
        /// Get the file name at a given index, if possible
        /// </summary>
        public string? GetFileName(int index) => _cabinet.GetFileName(index);

        /// <summary>
        /// Get the file group name at a given index, if possible
        /// </summary>
        public string? GetFileGroupName(int index) => _cabinet.GetFileGroupName(index);
    }
}
