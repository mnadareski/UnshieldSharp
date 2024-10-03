using System;
using System.IO;
using UnshieldSharp.Archive;
using UnshieldSharp.Cabinet;
using IA3 = SabreTools.Models.InstallShieldArchiveV3;

namespace Test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Setup options
            bool extract = true;
            bool outputInfo = false;
            bool script = false;
            string outputDirectory = string.Empty;

            // If we have no args, show the help and quit
            if (args == null || args.Length == 0)
            {
                DisplayHelp();
                Console.WriteLine("Press Enter to exit the program");
                Console.ReadLine();
                return;
            }

            // Loop through and process the options
            int firstFileIndex = 0;
            for (; firstFileIndex < args.Length; firstFileIndex++)
            {
                string arg = args[firstFileIndex];
                if (string.IsNullOrEmpty(arg))
                    continue;

                if (arg == "-?" || arg == "-h" || arg == "--help")
                {
                    DisplayHelp();
                    Console.WriteLine("Press Enter to exit the program");
                    Console.ReadLine();
                    return;
                }
                else if (arg == "-i" || arg == "--info")
                {
                    outputInfo = true;
                }
                else if (arg == "-n" || arg == "--no-extract")
                {
                    extract = false;
                }
                else if (arg == "-o" || arg == "--output")
                {
                    if (firstFileIndex == args.Length - 1)
                    {
                        Console.WriteLine("ERROR: No output directory provided");
                        DisplayHelp();
                        Console.WriteLine("Press Enter to exit the program");
                        Console.ReadLine();
                        return;
                    }

                    firstFileIndex++;
                    outputDirectory = args[firstFileIndex].Trim('"');
                }
                else if (arg == "-s" || arg == "--script")
                {
                    script = true;
                }
                else
                {
                    break;
                }
            }

            // If we have a no-op situation, just cancel out
            if (!outputInfo && !extract)
            {
                Console.WriteLine("Neither info nor extraction were selected, skipping all files...");

                // Only prompt to close when not in script mode
                if (!script)
                {
                    Console.WriteLine();
                    Console.WriteLine("Press Enter to exit the program");
                    Console.ReadLine();
                }
            }

            // Loop through all of the input files
            for (int i = firstFileIndex; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.EndsWith(".cab", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase))
                    ProcessCabinetPath(arg, outputInfo, extract, outputDirectory);
                else if (arg.EndsWith(".z", StringComparison.OrdinalIgnoreCase))
                    ProcessArchivePath(arg, outputInfo, extract, outputDirectory);
                else
                    Console.WriteLine($"{arg} is not a recognized file by extension");
            }

            // Only prompt to close when not in script mode
            if (!script)
            {
                Console.WriteLine();
                Console.WriteLine("Press Enter to exit the program");
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Display the help text to console
        /// </summary>
        private static void DisplayHelp()
        {
            Console.WriteLine("Test     - UnshieldSharp test program");
            Console.WriteLine();
            Console.WriteLine("This program was created to test the functionality found");
            Console.WriteLine("in the UnshieldSharp library. It is deliberately barebones");
            Console.WriteLine("due to this purpose. Currently this library supports both");
            Console.WriteLine("InstallShield cabinet and V3 archive files. Default behavior");
            Console.WriteLine("extracts the archive/cabinet to a named folder next to the");
            Console.WriteLine("original file.");
            Console.WriteLine();
            Console.WriteLine("Usage: Test.exe <options> <path/to/file> ...");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("    -?, -h, --help       Display this help text");
            Console.WriteLine("    -i, --info           Display archive/cabinet information");
            Console.WriteLine("    -n, --no-extract     Don't extract the archive");
            Console.WriteLine("    -o, --output <path>  Set the output directory for extraction");
            Console.WriteLine("    -s, --script         Script mode (doesn't prompt to close)");
            Console.WriteLine();
        }

        /// <summary>
        /// Process a single file path as an InstallShield archive
        /// </summary>
        /// <param name="file">Name of the file to process</param>
        /// <param name="outputInfo">True to display the cabinet information, false otherwise</param>
        /// <param name="extract">True to extract the cabinet, false otherwise</param>
        /// <param name="outputDirectory">Output directory for extraction</param>
        private static void ProcessArchivePath(string file, bool outputInfo, bool extract, string outputDirectory)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"{file} does not exist!");
                return;
            }

            var archive = new InstallShieldArchiveV3(file);
            if (archive?.Header == null)
            {
                Console.WriteLine($"{file} could not be opened as an InstallShield V3 Archive!");
                return;
            }

            if (outputInfo)
            {
                Console.WriteLine($"File count: {archive.Header.FileCount}");
                Console.WriteLine($"Archive size: {archive.Header.CompressedSize}");
                Console.WriteLine($"Directory count: {archive.Header.DirCount}");

                Console.WriteLine("Directory List:");
                foreach (IA3.Directory directory in archive.Directories)
                {
                    Console.WriteLine($"Directory: {directory.Name ?? string.Empty}, File Count: {directory.FileCount}");
                }

                Console.WriteLine("File list:");
                foreach (var cfile in archive.Files)
                {
                    Console.WriteLine($"File: {cfile.Key ?? string.Empty}, Compressed Size: {cfile.Value.CompressedSize}, Offset: {cfile.Value.Offset}");
                }
            }

            if (extract)
            {
                if (string.IsNullOrEmpty(outputDirectory))
                    outputDirectory = CreateOutdir(file) ?? string.Empty;

                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                foreach (var cfile in archive.Files)
                {
                    string filename = CleanPathSegment(cfile.Key);
                    string newfile = Path.Combine(outputDirectory, filename);

                    string? directoryName = Path.GetDirectoryName(newfile);
                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                        Directory.CreateDirectory(directoryName);

                    byte[]? fileContents = archive.Extract(cfile.Key, out string? error);
                    if (fileContents == null || !string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"Error detected while reading '{cfile.Key}': {error}");
                        continue;
                    }

                    Console.WriteLine($"Outputting file {cfile.Key} to {newfile}...");
                    using (FileStream fs = File.OpenWrite(newfile))
                    {
                        fs.Write(fileContents, 0, fileContents.Length);
                    }
                }
            }
        }

        /// <summary>
        /// Process a single file path as a cabinet file
        /// </summary>
        /// <param name="file">Name of the file to process</param>
        /// <param name="outputInfo">True to display the cabinet information, false otherwise</param>
        /// <param name="outputDirectory">Output directory for extraction</param>
        private static void ProcessCabinetPath(string file, bool outputInfo, bool extract, string outputDirectory)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"{file} does not exist!");
                return;
            }

            var cab = InstallShieldCabinet.Open(file);
            if (cab?.HeaderList == null)
            {
                Console.WriteLine($"{file} could not be opened as an InstallShield Cabinet!");
                return;
            }

            if (outputInfo)
            {
                // Component
                Console.WriteLine($"Component Count: {cab.HeaderList.ComponentCount}");
                for (int i = 0; i < cab.HeaderList.ComponentCount; i++)
                {
                    Console.WriteLine($"\tComponent {i}: {cab.HeaderList.GetComponentName(i)}");
                }

                // Directory -- TODO: multi-volume support...
                Console.WriteLine($"Directory Count: {cab.HeaderList.DirectoryCount}");
                for (int i = 0; i < cab.HeaderList.DirectoryCount; i++)
                {
                    Console.WriteLine($"\tDirectory {i}: {cab.HeaderList.GetDirectoryName(i)}");
                }

                // File -- TODO: multi-volume support...
                Console.WriteLine($"File Count: {cab.HeaderList.FileCount}");
                for (int i = 0; i < cab.HeaderList.FileCount; i++)
                {
                    Console.WriteLine($"\tFile {i}: {cab.HeaderList.GetFileName(i)}");
                }

                // File Group
                Console.WriteLine($"File Group Count: {cab.HeaderList.FileGroupCount}");
                for (int i = 0; i < cab.HeaderList.FileGroupCount; i++)
                {
                    Console.WriteLine($"\tFile Group {i}: {cab.HeaderList.GetFileGroupName(i)}");
                }
            }

            if (extract)
            {
                if (string.IsNullOrEmpty(outputDirectory))
                    outputDirectory = CreateOutdir(file) ?? string.Empty;

                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                for (int i = 0; i < cab.HeaderList.FileCount; i++)
                {
                    // Get and clean each path segment
                    string filename = CleanPathSegment(cab.HeaderList.GetFileName(i));
                    string directory = CleanPathSegment(cab.HeaderList.GetDirectoryName((int)cab.HeaderList.GetFileDirectoryIndex(i)));
                    string fileGroup = CleanPathSegment(FindFileGroup(cab, i));

                    // Assemble the complete output path
#if NET20 || NET35
                    string newfile = Path.Combine(Path.Combine(Path.Combine(outputDirectory, fileGroup), directory), filename);
#else
                    string newfile = Path.Combine(outputDirectory, fileGroup, directory, filename);
#endif

                    // Ensure the output directory exists
                    string? directoryName = Path.GetDirectoryName(newfile);
                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                        Directory.CreateDirectory(directoryName);

                    Console.WriteLine($"Outputting file at index {i} to {newfile}...");
                    cab.FileSave(i, newfile);
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
    
        /// <summary>
        /// Find the file group for a given file index
        /// </summary>
        /// <param name="cab">Cabinet containing the file index</param>
        /// <param name="fileIndex">Index of the file to check</param>
        /// <returns>File group name on success, empty string on error</returns>
        private static string FindFileGroup(InstallShieldCabinet? cab, int fileIndex)
        {
            // The following code has been disabled until file group parsing
            // is fixed. See below for more details.
            return string.Empty;

            // // Handle an invalid cabinet
            // if (cab?.HeaderList == null)
            //     return string.Empty;

            // // Handle an invalid file index
            // if (fileIndex < 0 || fileIndex > cab.HeaderList.FileCount)
            //     return string.Empty;

            // // Search all file groups
            // for (int i = 0; i < cab.HeaderList.FileGroupCount; i++)
            // {
            //     // Get the file group for the index
            //     var fileGroup = cab.HeaderList.GetFileGroup(i);
            //     if (fileGroup == null)
            //         continue;

            //     // Due to a bug in the processing code, FirstFile and LastFile
            //     // are shifted by one Int32 value. This means that the current
            //     // FirstFile is actually LastFile and LastFile is another value
            //     // entirely.

            //     // Check the range in the file group
            //     if (fileGroup.FirstFile > fileIndex || fileGroup.LastFile < fileIndex)
            //         continue;

            //     // Get and return the file group name
            //     return cab.HeaderList.GetFileGroupName(i) ?? string.Empty;
            // }

            // // If no group was found
            // return string.Empty;
        }
    }
}
