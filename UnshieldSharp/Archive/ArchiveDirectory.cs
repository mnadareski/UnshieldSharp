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
        public string Name { get; set; }

        /// <summary>
        /// Total number of files in the directory
        /// </summary>
        public ushort FileCount { get; set; }
    }
}