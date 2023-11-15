using System.IO;
using System.Linq;
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
        /// Get the component name at a given index, if possible
        /// </summary>
        public string? GetComponentName(int index)
        {
            if (index < 0 || index >= _cabinet.Model.Components!.Length)
                return null;

            var component = _cabinet.Model.Components![index];
            if (component?.Identifier == null)
                return null;

            return component.Identifier.Replace('\\', '/');
        }

        /// <summary>
        /// Get the directory name at a given index, if possible
        /// </summary>
        public string? GetDirectoryName(int index)
        {
            if (index < 0 || index >= _cabinet.Model.DirectoryNames!.Length)
                return null;

            return _cabinet.Model.DirectoryNames[index];
        }

        /// <summary>
        /// Get the file descriptor at a given index, if possible
        /// </summary>
        public FileDescriptor? GetFileDescriptor(int index)
        {
            if (index < 0 || index >= _cabinet.Model.FileDescriptors!.Length)
                return null;

            return _cabinet.Model.FileDescriptors[index];
        }

        /// <summary>
        /// Get the file group at a given index, if possible
        /// </summary>
        public FileGroup? GetFileGroup(int index)
        {
            if (index < 0 || index >= _cabinet.Model.FileGroups!.Length)
                return null;

            return _cabinet.Model.FileGroups[index];
        }

        /// <summary>
        /// Get the file group at a given name, if possible
        /// </summary>
        public FileGroup? GetFileGroup(string name)
        {
            return _cabinet.Model.FileGroups!.FirstOrDefault(fg => fg != null && string.Equals(fg.Name, name));
        }

        /// <summary>
        /// Get the file name at a given index, if possible
        /// </summary>
        public string? GetFileName(int index)
        {
            var descriptor = GetFileDescriptor(index);
            if (descriptor == null || descriptor.Flags.HasFlag(FileFlags.FILE_INVALID))
                return null;

            return descriptor.Name;
        }

        /// <summary>
        /// Get the file group name at a given index, if possible
        /// </summary>
        public string? GetFileGroupName(int index)
        {
            if (index < 0 || index >= _cabinet.Model.FileGroups!.Length)
                return null;

            var fileGroup = _cabinet.Model.FileGroups[index];
            if (fileGroup == null)
                return null;

            return fileGroup.Name;
        }
    }
}
