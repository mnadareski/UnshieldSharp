using System;
using System.IO;
using System.Linq;
using UnshieldSharp;

namespace Test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                var cab = UnshieldCabinet.Open(arg);

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

                // Extract
                string baseDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(arg)), Path.GetFileNameWithoutExtension(arg));
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

            Console.WriteLine();
            Console.WriteLine("Program execution finished!");
            Console.ReadLine();
        }
    }
}
