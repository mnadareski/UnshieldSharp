﻿using System;
using System.IO;
using UnshieldSharp;

namespace Test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Setup options
            bool extract = true;
            bool outputInfo = false;
            string outputDirectory = string.Empty;
            bool useOld = false;

            // If we have no args, show the help and quit
            if (args == null || args.Length == 0)
            {
                DisplayHelp();
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
                else if (arg == "-u" || arg == "--use-old")
                {
                    useOld = true;
                }
                else if (arg == "-o" || arg == "--output")
                {
                    if (firstFileIndex == args.Length - 1)
                    {
                        Console.WriteLine("ERROR: No output directory provided");
                        DisplayHelp();
                        return;
                    }

                    firstFileIndex++;
                    outputDirectory = args[firstFileIndex].Trim('"');
                }
                else
                {
                    break;
                }
            }

            // If we have a no-op situation, just cancel out
            if (!outputInfo && !extract)
                Console.WriteLine("Neither info nor extraction were selected, skipping all files...");

            // Loop through all of the input files
            for (int i = firstFileIndex; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.EndsWith(".cab", StringComparison.OrdinalIgnoreCase))
                    ProcessCabinetPath(arg, outputInfo, extract, useOld, outputDirectory);
                else if (arg.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase))
                    ProcessCabinetPath(arg, outputInfo, extract, useOld, outputDirectory);
                else
                    Console.WriteLine($"{arg} is not a recognized file by extension");
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
            Console.WriteLine("due to this purpose. Default behavior extracts the cabinet");
            Console.WriteLine("to a named folder next to the original file.");
            Console.WriteLine();
            Console.WriteLine("Usage: Test.exe <options> <path/to/file> ...");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("    -?, -h, --help       Display this help text");
            Console.WriteLine("    -i, --info           Display cabinet information");
            Console.WriteLine("    -n, --no-extract     Don't extract the cabinet");
            Console.WriteLine("    -o, --output <path>  Set the output directory for extraction");
            Console.WriteLine("    -u, --use-old        Use old extraction method");
            Console.WriteLine();
        }

        /// <summary>
        /// Process a single file path as a cabinet file
        /// </summary>
        /// <param name="file">Name of the file to process</param>
        /// <param name="outputInfo">True to display the cabinet information, false otherwise</param>
        /// <param name="outputDirectory">Output directory for extraction</param>
        private static void ProcessCabinetPath(string file, bool outputInfo, bool extract, bool useOld, string outputDirectory)
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
                    string directory = CleanPathSegment(cab.HeaderList.GetDirectoryName((int)cab.HeaderList.GetDirectoryIndexFromFile(i)));
                    string fileGroup = CleanPathSegment(cab.HeaderList.GetFileGroupNameFromFile(i));

                    // Assemble the complete output path
#if NET20 || NET35
                    string newfile = Path.Combine(Path.Combine(outputDirectory, directory), filename);
#else
                    string newfile = Path.Combine(outputDirectory, directory, filename);
#endif

                    // Ensure the output directory exists
                    string? directoryName = Path.GetDirectoryName(newfile);
                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                        Directory.CreateDirectory(directoryName);

                    Console.WriteLine($"Outputting file at index {i} to {newfile}...");
                    cab.FileSave(i, newfile, useOld);
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
