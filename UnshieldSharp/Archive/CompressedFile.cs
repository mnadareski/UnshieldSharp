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
        public string Name { get; set; }

        /// <summary>
        /// Full internal path of the compressed file
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Size of the compressed file in bytes
        /// </summary>
        public uint CompressedSize { get; set; }

        /// <summary>
        /// Offset of the file within the parent archive
        /// </summary>
        public uint Offset { get; set; }
    }
}