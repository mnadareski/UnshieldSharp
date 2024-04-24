using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class Header : SabreTools.Serialization.Wrappers.InstallShieldCabinet
    {
        #region Extension Fields

        /// <summary>
        /// Reference to the next cabinet header
        /// </summary>
        public Header? Next { get; set; }

        #endregion

        #region Passthrough Properties

        /// <summary>
        /// Number of components in the cabinet set
        /// </summary>
        public int ComponentCount => Model.Components!.Length;

        /// <summary>
        /// Number of directories in the cabinet set
        /// </summary>
        public ushort DirectoryCount => Model.Descriptor!.DirectoryCount;

        /// <summary>
        /// Number of files in the cabinet set
        /// </summary>
        public uint FileCount => Model.Descriptor!.FileCount;

        /// <summary>
        /// Number of file groups in the cabinet set
        /// </summary>
        public int FileGroupCount => Model.FileGroups!.Length;

        #endregion

        public Header(SabreTools.Models.InstallShieldCabinet.Cabinet? model, Stream? data)
            : base(model, data)
        {
        }

        /// <summary>
        /// Create a new Header from a stream and an index
        /// </summary>
        public static Header? Create(Stream stream, int index)
        {
            stream.Seek(index, SeekOrigin.Begin);
            var cabinet = Create(stream);
            if (cabinet == null)
                return null;

            return new Header(cabinet.Model, stream);
        }
    }
}
