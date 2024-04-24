/* unshieldv3 -- extract InstallShield V3 archives.
Copyright (c) 2019 Wolfgang Frisch <wfrisch@riseup.net>
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnshieldSharp.Blast;
using IA3 = SabreTools.Models.InstallShieldArchiveV3;

namespace UnshieldSharp.Archive
{
    /// <summary>
    /// InstallShield V3 .Z archive reader.
    /// 
    /// Reference (de)compressor: https://www.sac.sk/download/pack/icomp95.zip
    /// </summary>
    /// <remarks>
    /// Assumes little-endian byte order. Don't bother,
    /// until someone verifies that blast.c works on big-endian.
    /// </remarks>
    public unsafe class InstallShieldArchiveV3
    {
        /// <summary>
        /// Archive header information
        /// </summary>
        public IA3.Header? Header { get; private set; }

        /// <summary>
        /// Currently loaded file path
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// List of directories found in the archive
        /// </summary>
        public List<IA3.Directory> Directories { get; private set; } = [];

        /// <summary>
        /// List of files found in the archive
        /// </summary>
        public Dictionary<string, IA3.File> Files { get; private set; } = [];

        /// <summary>
        /// Stream representing the input archive
        /// </summary>
        private Stream? inputStream;

        /// <summary>
        /// Data offset for all archives
        /// </summary>
        private const uint DataStart = 255;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">File path to try to load</param>
        public InstallShieldArchiveV3(string path)
        {
            FilePath = path;
            LoadFile();
        }

        /// <summary>
        /// Determines if the archive contains a particular path
        /// </summary>
        /// <param name="fullPath">Internal full path for the file to check</param>
        /// <returns>True if the full path exists, false otherwise</returns>
        public bool Exists(string fullPath) => Files.Any(f => f.Key.Contains(fullPath));

        /// <summary>
        /// Extract a file to byte array using the path
        /// </summary>
        /// <param name="fullPath">Internal full path for the file to extract</param>
        /// <returns>Uncompressed data and no error string on success, null data and an error string otherwise</returns>
        public (byte[]? data, string? err) Extract(string fullPath)
        {
            // If the file isn't in the archive, we can't extract it
            if (!Exists(fullPath))
                return (null, $"Path '{fullPath}' does not exist in the archive");

            // Get a local reference to the file we care about
            IA3.File file = Files[fullPath];

            // Attempt to read the compressed data
            inputStream!.Seek(DataStart + file.Offset, SeekOrigin.Begin);
            byte[] compressedData = new byte[file.CompressedSize];
            int read = inputStream.Read(compressedData, 0, (int)file.CompressedSize);
            if (read != (int)file.CompressedSize)
                return (null, "Could not read all required data");

            // Decompress the data
            var output = new List<byte>();
            int ret = BlastDecoder.Blast(compressedData, output);
            if (ret != 0)
                return (null, $"Blast error: {ret}");

            // Return the decompressed data
            return ([.. output], null);
        }

        /// <summary>
        /// Load the file set as the current path
        /// </summary>
        /// <returns>Success and error strings, if applicable</returns>
        private (bool success, string? err) LoadFile()
        {
            // If the file doesn't exist, we can't do anything
            if (!File.Exists(FilePath))
                return (false, $"File '{FilePath}' does not exist");

            // Attempt to open the file for reading
            try
            {
                inputStream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch
            {
                return (false, "Cannot open file");
            }

            // Create the header from the input file
            Header = SabreTools.Serialization.Deserializers.InstallShieldArchiveV3.ParseHeader(inputStream);
            if (Header == null)
                return (false, "Header could not be read or was invalid");

            // Move to the TOC
            inputStream.Seek(Header.TocAddress, SeekOrigin.Begin);

            // Read all directory info
            for (int i = 0; i < Header.DirCount; i++)
            {
                var dir = SabreTools.Serialization.Deserializers.InstallShieldArchiveV3.ParseDirectory(inputStream, out uint chunkSize);
                if (dir == null)
                    break;

                dir.ChunkSize = (ushort)chunkSize;
                inputStream.Seek(dir.ChunkSize - dir.Name!.Length - 6, SeekOrigin.Current);
                Directories.Add(dir);
            }

            // For each directory, read all file info
            uint accumOffset = 0;
            foreach (IA3.Directory directory in Directories)
            {
                for (int i = 0; i < directory.FileCount; i++)
                {
                    // Read in the file information
                    var file = SabreTools.Serialization.Deserializers.InstallShieldArchiveV3.ParseFile(inputStream);
                    if (file == null)
                        break;

                    inputStream.Seek(file.ChunkSize - file.Name!.Length - 30, SeekOrigin.Current);

                    // Determine the full path of the internal file
                    string fullPath;
                    if (!string.IsNullOrEmpty(directory.Name) && directory.Name!.Length > 0)
                        fullPath = Path.Combine(directory.Name, file.Name);
                    else
                        fullPath = file.Name;

                    // Add the file to the list
                    Files[fullPath] = file;

                    // Accumulate the new offset
                    accumOffset += file.CompressedSize;
                }
            }

            return (true, null);
        }
    }
}