using System;
using System.IO;
using SabreTools.CommandLine;
using SabreTools.CommandLine.Inputs;
using SabreTools.Wrappers;

namespace UnshieldSharp.Features
{
    internal sealed class MainFeature : Feature
    {
        #region Feature Definition

        public const string DisplayName = "main";

        /// <remarks>Flags are unused</remarks>
        private static readonly string[] _flags = [];

        /// <remarks>Description is unused</remarks>
        private const string _description = "";

        #endregion

        #region Inputs

        private const string _debugName = "debug";
        internal readonly FlagInput DebugInput = new(_debugName, ["-d", "--debug"], "Output debug information");

        private const string _infoName = "info";
        internal readonly FlagInput InfoInput = new(_infoName, ["-i", "--info"], "Display cabinet information");

        private const string _noExtractName = "no-extract";
        internal readonly FlagInput NoExtractInput = new(_noExtractName, ["-n", "--no-extract"], "Don't extract the cabinet");

        private const string _outputDirectoryName = "output-directory";
        internal readonly StringInput OutputDirectoryInput = new(_outputDirectoryName, ["-o", "--output"], "Set the output directory for extraction");

        #endregion

        #region Properties

        /// <summary>
        /// Enable debug output for relevant operations
        /// </summary>
        public bool OutputInfo { get; private set; }

        /// <summary>
        /// Enable extraction for the input file
        /// </summary>
        public bool Extract { get; private set; }

        /// <summary>
        /// Output debug information
        /// </summary>
        public bool Debug { get; private set; }

        /// <summary>
        /// Output path for cabinet extraction
        /// </summary>
        public string OutputDirectory { get; private set; } = string.Empty;

        #endregion

        public MainFeature()
            : base(DisplayName, _flags, _description)
        {
            RequiresInputs = true;

            Add(InfoInput);
            Add(NoExtractInput);
            Add(OutputDirectoryInput);
            Add(DebugInput);
        }

        /// <inheritdoc/>
        public override bool Execute()
        {
            // Get the options from the arguments
            OutputInfo = GetBoolean(_infoName);
            Extract = !GetBoolean(_noExtractName);
            OutputDirectory = GetString(_outputDirectoryName) ?? string.Empty;
            Debug = GetBoolean(_debugName);

            // If we have a no-op situation, just cancel out
            if (!OutputInfo && !Extract)
            {
                Console.WriteLine("Neither info nor extraction were selected, skipping all files...");
                return false;
            }

            // Loop through all of the input files
            for (int i = 0; i < Inputs.Count; i++)
            {
                string arg = Inputs[i];
                if (arg.EndsWith(value: ".cab", StringComparison.OrdinalIgnoreCase))
                    ProcessCabinetPath(arg);
                else if (arg.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase))
                    ProcessCabinetPath(arg);
                else
                    Console.WriteLine($"{arg} is not a recognized file by extension");
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool VerifyInputs() => Inputs.Count > 0;

        /// <summary>
        /// Process a single file path as a cabinet file
        /// </summary>
        /// <param name="file">Name of the file to process</param>
        private void ProcessCabinetPath(string file)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"{file} does not exist!");
                return;
            }

            using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var cab = InstallShieldCabinet.Create(fs);
            if (cab is null)
            {
                Console.WriteLine($"{file} could not be opened as an InstallShield Cabinet!");
                return;
            }

            if (OutputInfo)
            {
                // Component
                Console.WriteLine($"Component Count: {cab.ComponentCount}");
                for (int i = 0; i < cab.ComponentCount; i++)
                {
                    Console.WriteLine($"\tComponent {i}: {cab.GetComponentName(i)}");
                }

                // Directory -- TODO: multi-volume support...
                Console.WriteLine($"Directory Count: {cab.DirectoryCount}");
                for (int i = 0; i < cab.DirectoryCount; i++)
                {
                    Console.WriteLine($"\tDirectory {i}: {cab.GetDirectoryName(i)}");
                }

                // File -- TODO: multi-volume support...
                Console.WriteLine($"File Count: {cab.FileCount}");
                for (int i = 0; i < cab.FileCount; i++)
                {
                    Console.WriteLine($"\tFile {i}: {cab.GetFileName(i)}");
                }

                // File Group
                Console.WriteLine($"File Group Count: {cab.FileGroupCount}");
                for (int i = 0; i < cab.FileGroupCount; i++)
                {
                    Console.WriteLine($"\tFile Group {i}: {cab.GetFileGroupName(i)}");
                }
            }

            if (Extract)
            {
                if (string.IsNullOrEmpty(OutputDirectory))
                    OutputDirectory = CreateOutdir(file) ?? string.Empty;

                if (!Directory.Exists(OutputDirectory))
                    Directory.CreateDirectory(OutputDirectory);

                cab.Extract(OutputDirectory, Debug);
            }
        }

        /// <summary>
        /// Generate the output directory path, if possible
        /// </summary>
        /// <param name="input">Input path to generate from</param>
        /// <returns>Output directory path on success, null on error</returns>
        private static string? CreateOutdir(string input)
        {
            // If the file path is not valid
            if (string.IsNullOrEmpty(input) || !File.Exists(input))
                return null;

            // Get the full path for the input, if possible
            input = Path.GetFullPath(input);

            // Get the directory name and filename without extension for processing
            string? directoryName = Path.GetDirectoryName(input);
            string? fileNameWithoutExtension = Path.GetFileNameWithoutExtension(input);

            // Return an output path based on the two parts
            if (string.IsNullOrEmpty(directoryName) && string.IsNullOrEmpty(fileNameWithoutExtension))
                return null;
            else if (string.IsNullOrEmpty(directoryName) && !string.IsNullOrEmpty(fileNameWithoutExtension))
                return fileNameWithoutExtension;
            else if (!string.IsNullOrEmpty(directoryName) && string.IsNullOrEmpty(fileNameWithoutExtension))
                return directoryName;
            else
                return Path.Combine(directoryName!, fileNameWithoutExtension!);
        }
    }
}
