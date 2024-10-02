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
#if NET40_OR_GREATER || NETCOREAPP
using System.Linq;
#endif
using SabreTools.Compression.Blast;
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
            LoadFile(out _);
        }

        /// <summary>
        /// Determines if the archive contains a particular path
        /// </summary>
        /// <param name="fullPath">Internal full path for the file to check</param>
        /// <returns>True if the full path exists, false otherwise</returns>
#if NET20 || NET35
        public bool Exists(string fullPath)
        {
            foreach (var f in Files)
            {
                if (f.Key.Contains(fullPath))
                    return true;
            }

            return false;
        }
#else
        public bool Exists(string fullPath) => Files.Any(f => f.Key.Contains(fullPath));
#endif

        /// <summary>
        /// Extract a file to byte array using the path
        /// </summary>
        /// <param name="fullPath">Internal full path for the file to extract</param>
        /// <returns>Uncompressed data on success, null otherwise</returns>
        public byte[]? Extract(string fullPath, out string? err)
        {
            // If the file isn't in the archive, we can't extract it
            if (!Exists(fullPath))
            {
                err = $"Path '{fullPath}' does not exist in the archive";
                return null;
            }

            // Get a local reference to the file we care about
            IA3.File file = Files[fullPath];

            // Attempt to read the compressed data
            inputStream!.Seek(DataStart + file.Offset, SeekOrigin.Begin);
            byte[] compressedData = new byte[file.CompressedSize];
            int read = inputStream.Read(compressedData, 0, (int)file.CompressedSize);
            if (read != (int)file.CompressedSize)
            {
                err = "Could not read all required data";
                return null;
            }

            // Decompress the data
            var output = new List<byte>();
            int ret = BlastDecoder.Blast(compressedData, output);
            if (ret != 0)
            {
                err = $"Blast error: {ret}";
                return null;
            }

            // Return the decompressed data
            err = null;
            return [.. output];
        }

        /// <summary>
        /// Load the file set as the current path
        /// </summary>
        /// <returns>Status of loaded file</returns>
        private bool LoadFile(out string? err)
        {
            // If the file doesn't exist, we can't do anything
            if (!File.Exists(FilePath))
            {
                err = $"File '{FilePath}' does not exist";
                return false;
            }

            // Attempt to open the file for reading
            try
            {
                inputStream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch
            {
                err = "Cannot open file";
                return false;
            }

            // Create the header from the input file
            Header = SabreTools.Serialization.Deserializers.InstallShieldArchiveV3.ParseHeader(inputStream);
            if (Header == null)
            {
                err = "Header could not be read or was invalid";
                return false;
            }

            // Move to the TOC
            inputStream.Seek(Header.TocAddress, SeekOrigin.Begin);

            // Read all directory info
            for (int i = 0; i < Header.DirCount; i++)
            {
                var dir = SabreTools.Serialization.Deserializers.InstallShieldArchiveV3.ParseDirectory(inputStream);
                if (dir == null)
                    break;

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

            err = null;
            return true;
        }
    }
}