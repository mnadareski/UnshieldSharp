using System;
using System.IO;
using System.Linq;
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
                return;

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
                    string newfile = Path.Combine(outputDirectory, cfile.Key!.Replace('\\', '/'));

                    string? directoryName = Path.GetDirectoryName(newfile);
                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                        Directory.CreateDirectory(directoryName);

                    (byte[]? fileContents, string? error) = archive.Extract(cfile.Key);
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
                return;

            var cab = InstallShieldCabinet.Open(file);
            if (cab == null)
            {
                Console.WriteLine($"{file} could not be opened as an InstallShield Cabinet!");
                return;
            }

            if (outputInfo)
            {
                // Component
                Console.WriteLine($"Component Count: {cab.ComponentCount}");
                for (int i = 0; i < cab.ComponentCount; i++)
                    Console.WriteLine($"\tComponent {i}: {cab.ComponentName(i)}");

                // Directory
                Console.WriteLine($"Directory Count: {cab.DirectoryCount}");
                for (int i = 0; i < cab.DirectoryCount; i++)
                    Console.WriteLine($"\tDirectory {i}: {cab.DirectoryName(i)}");

                // File
                Console.WriteLine($"File Count: {cab.FileCount}");
                for (int i = 0; i < cab.FileCount; i++)
                    Console.WriteLine($"\tFile {i}: {cab.FileName(i)}");

                // File Group
                Console.WriteLine($"File Group Count: {cab.FileGroupCount}");
                for (int i = 0; i < cab.FileGroupCount; i++)
                    Console.WriteLine($"\tFile Group {i}: {cab.FileGroupName(i)}");
            }

            if (extract)
            {
                if (string.IsNullOrEmpty(outputDirectory))
                    outputDirectory = CreateOutdir(file) ?? string.Empty;

                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                for (int i = 0; i < cab.FileCount; i++)
                {
                    char[]? filenameChars = cab.FileName(i)?.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)?.ToArray();
                    string filename = filenameChars != null ? new(filenameChars) : string.Empty;
                    char[]? directoryChars = cab.DirectoryName((int)cab.FileDirectory(i))?.Select(c => Path.GetInvalidPathChars().Contains(c) ? '_' : c)?.ToArray();
                    string directory = directoryChars != null ? new(directoryChars) : string.Empty;
#if NET20 || NET35
                    string newfile = Path.Combine(Path.Combine(outputDirectory, directory), filename);
#else
                    string newfile = Path.Combine(outputDirectory, directory, filename);
#endif

                    string? directoryName = Path.GetDirectoryName(newfile);
                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                        Directory.CreateDirectory(directoryName);

                    Console.WriteLine($"Outputting file at index {i} to {newfile}...");
                    cab.FileSave(i, newfile);
                }
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
