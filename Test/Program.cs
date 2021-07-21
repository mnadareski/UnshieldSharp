using System;
using System.IO;
using System.Linq;
using UnshieldSharp.Archive;
using UnshieldSharp.Cabinet;

namespace Test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.EndsWith(".cab", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase))
                    ProcessCabinetPath(arg);
                else if (arg.EndsWith(".z", StringComparison.OrdinalIgnoreCase))
                    ProcessArchivePath(arg);
            }

            Console.WriteLine();
            Console.WriteLine("Program execution finished!");
            Console.ReadLine();
        }

        /// <summary>
        /// Process a single file path as an InstallShield archive
        /// </summary>
        /// <param name="file">Name of the file to process</param>
        /// <param name="outputInfo">True to display the cabinet information, false otherwise</param>
        /// <param name="extract">True to extract the cabinet, false otherwise</param>
        private static void ProcessArchivePath(string file, bool outputInfo = true, bool extract = true)
        {
            if (!File.Exists(file))
                return;

            var archive = new InstallShieldArchiveV3(file);
            if (outputInfo)
            {
                Console.WriteLine($"File count: {archive.Header.FileCount}");
                Console.WriteLine($"Archive size: {archive.Header.ArchiveSize}");
                Console.WriteLine($"Directory count: {archive.Header.DirCount}");

                Console.WriteLine("Directory List:");
                foreach (ArchiveDirectory directory in archive.Directories)
                    Console.WriteLine($"Directory: {directory.Name ?? string.Empty}, File Count: {directory.FileCount}");

                Console.WriteLine("File list:");
                foreach (CompressedFile cfile in archive.Files.Select(kvp => kvp.Value))
                    Console.WriteLine($"File: {cfile.FullPath ?? string.Empty}, Compressed Size: {cfile.CompressedSize}, Offset: {cfile.Offset}");
            }

            if (extract)
            {
                string baseDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)), Path.GetFileNameWithoutExtension(file));
                if (!Directory.Exists(baseDirectory))
                    Directory.CreateDirectory(baseDirectory);

                foreach (CompressedFile internalFile in archive.Files.Select(kvp => kvp.Value))
                {
                    string newfile = Path.Combine(baseDirectory, internalFile.FullPath);

                    if (!Directory.Exists(Path.GetDirectoryName(newfile)))
                        Directory.CreateDirectory(Path.GetDirectoryName(newfile));

                    (byte[] fileContents, string error) = archive.Extract(internalFile.FullPath);
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Console.WriteLine($"Error detected while reading '{internalFile.FullPath}': {error}");
                        continue;
                    }

                    Console.WriteLine($"Outputting file {internalFile.FullPath} to {newfile}...");
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
        /// <param name="extract">True to extract the cabinet, false otherwise</param>
        private static void ProcessCabinetPath(string file, bool outputInfo = true, bool extract = true)
        {
            if (!File.Exists(file))
                return;

            var cab = InstallShieldCabinet.Open(file);
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
                // Extract
                string baseDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)), Path.GetFileNameWithoutExtension(file));
                if (!Directory.Exists(baseDirectory))
                    Directory.CreateDirectory(baseDirectory);

                for(int i = 0; i < cab.FileCount; i++)
                {
                    string filename = new string(cab.FileName(i).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
                    string directory = cab.DirectoryName(cab.FileDirectory(i));
                    string newfile = Path.Combine(baseDirectory, directory, filename);

                    if (!Directory.Exists(Path.GetDirectoryName(newfile)))
                        Directory.CreateDirectory(Path.GetDirectoryName(newfile));

                    Console.WriteLine($"Outputting file at index {i} to {newfile}...");
                    cab.FileSave(i, newfile);
                }
            }
        }
    }
}
