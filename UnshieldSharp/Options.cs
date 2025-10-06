namespace UnshieldSharp
{
    /// <summary>
    /// Set of options for the executable
    /// </summary>
    internal sealed class Options
    {
        /// <summary>
        /// Enable debug output for relevant operations
        /// </summary>
        public bool OutputInfo { get; set; }

        /// <summary>
        /// Enable extraction for the input file
        /// </summary>
        public bool Extract { get; set; }

        /// <summary>
        /// Enable using old extraction method
        /// </summary>
        public bool UseOld { get; set; }

        /// <summary>
        /// Output path for cabinet extraction
        /// </summary>
        public string OutputDirectory { get; set; } = string.Empty;
    }
}