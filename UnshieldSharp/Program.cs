using System;
using System.Collections.Generic;
using System.IO;
using SabreTools.CommandLine;
using SabreTools.CommandLine.Inputs;
using SabreTools.Serialization.Wrappers;

namespace UnshieldSharp
{
    public class Program
    {
        #region Constants

        private const string _helpName = "help";
        private const string _infoName = "info";
        private const string _noExtractName = "no-extract";
        private const string _outputDirectoryName = "output-directory";
        private const string _useOldName = "use-old";

        #endregion

        public static void Main(string[] args)
        {
            // Create the command set
            var commandSet = CreateCommands();

            // If we have no args, show the help and quit
            if (args == null || args.Length == 0)
            {
                commandSet.OutputGenericHelp();
                return;
            }

            // Loop through and process the options
            int firstFileIndex = 0;
            for (; firstFileIndex < args.Length; firstFileIndex++)
            {
                string arg = args[firstFileIndex];

                var input = commandSet.GetTopLevel(arg);
                if (input == null)
                    break;

                input.ProcessInput(args, ref firstFileIndex);
            }

            // If help was specified
            if (commandSet.GetBoolean(_helpName))
            {
                commandSet.OutputGenericHelp();
                return;
            }

            // Get the options from the arguments
            var options = new Options
            {
                OutputInfo = commandSet.GetBoolean(_infoName),
                Extract = !commandSet.GetBoolean(_noExtractName),
                UseOld = commandSet.GetBoolean(_useOldName),
                OutputDirectory = commandSet.GetString(_outputDirectoryName) ?? string.Empty,
            };

            // If we have a no-op situation, just cancel out
            if (!options.OutputInfo && !options.Extract)
            {
                Console.WriteLine("Neither info nor extraction were selected, skipping all files...");
                return;
            }

            // Loop through all of the input files
            for (int i = firstFileIndex; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.EndsWith(".cab", StringComparison.OrdinalIgnoreCase))
                    ProcessCabinetPath(arg, options);
                else if (arg.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase))
                    ProcessCabinetPath(arg, options);
                else
                    Console.WriteLine($"{arg} is not a recognized file by extension");
            }
        }

        /// <summary>
        /// Create the command set for the program
        /// </summary>
        private static CommandSet CreateCommands()
        {
            List<string> header = [
                "Usage: UnshieldSharp <options> file|directory ...",
                string.Empty,
            ];

            var commandSet = new CommandSet(header);

            commandSet.Add(new FlagInput(_helpName, ["-?", "-h", "--help"], "Display this help text"));
            commandSet.Add(new FlagInput(_infoName, ["-i", "--info"], "Display cabinet information"));
            commandSet.Add(new FlagInput(_noExtractName, ["-n", "--no-extract"], "Don't extract the cabinet"));
            commandSet.Add(new StringInput(_outputDirectoryName, ["-o", "--output"], "Set the output directory for extraction"));
            commandSet.Add(new FlagInput(_useOldName, ["-u", "--use-old"], "Use old extraction method"));

            return commandSet;
        }

        /// <summary>
        /// Process a single file path as a cabinet file
        /// </summary>
        /// <param name="file">Name of the file to process</param>
        /// <param name="options">Options containing all settings</param>
        private static void ProcessCabinetPath(string file, Options options)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"{file} does not exist!");
                return;
            }

            using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var cab = InstallShieldCabinet.Create(fs);
            if (cab == null)
            {
                Console.WriteLine($"{file} could not be opened as an InstallShield Cabinet!");
                return;
            }

            if (options.OutputInfo)
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

            if (options.Extract)
            {
                if (string.IsNullOrEmpty(options.OutputDirectory))
                    options.OutputDirectory = CreateOutdir(file) ?? string.Empty;

                if (!Directory.Exists(options.OutputDirectory))
                    Directory.CreateDirectory(options.OutputDirectory);

                for (int i = 0; i < cab.FileCount; i++)
                {
                    // Get and clean each path segment
                    string filename = CleanPathSegment(cab.GetFileName(i));
                    string directory = CleanPathSegment(cab.GetDirectoryName((int)cab.GetDirectoryIndexFromFile(i)));
                    string fileGroup = CleanPathSegment(cab.GetFileGroupNameFromFile(i));

                    // Assemble the complete output path
#if NET20 || NET35
                    string newfile = Path.Combine(Path.Combine(options.OutputDirectory, directory), filename);
#else
                    string newfile = Path.Combine(options.OutputDirectory, directory, filename);
#endif

                    // Ensure the output directory exists
                    string? directoryName = Path.GetDirectoryName(newfile);
                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                        Directory.CreateDirectory(directoryName);

                    Console.WriteLine($"Outputting file at index {i} to {newfile}...");
                    cab.FileSave(i, newfile, options.UseOld);
                }
            }
        }

        /// <summary>
        /// Clean a path segment by normalizing as much as possible
        /// </summary>
        /// <param name="segment">Path segment as provided by the cabinet</param>
        /// <returns>Cleaned path segment, if possible</returns>
        private static string CleanPathSegment(string? segment)
        {
            // Invalid pieces are returned as empty strings
            if (segment == null)
                return string.Empty;

            // Replace directory separators
            if (Path.DirectorySeparatorChar == '\\')
                segment = segment.Replace('/', '\\');
            else if (Path.DirectorySeparatorChar == '/')
                segment = segment.Replace('\\', '/');

            // Replace invalid path characters
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                segment = segment.Replace(c, '_');
            }

            return segment;
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
