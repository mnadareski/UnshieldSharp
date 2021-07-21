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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnshieldSharp.Blast;

namespace UnshieldSharp.Archive
{
    /* InstallShield V3 .Z archive reader.
    *
    * Reference (de)compressor: https://www.sac.sk/download/pack/icomp95.zip
    *
    * Assumes little-endian byte order. Don't bother,
    * until someone verifies that blast.c works on big-endian. */
    public unsafe class InstallShieldArchiveV3
    {
        /// <summary>
        /// Archive header information
        /// </summary>
        public Header Header { get; private set; }

        /// <summary>
        /// Currently loaded file path
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// List of directories found in the archive
        /// </summary>
        public List<ArchiveDirectory> Directories { get; private set; }

        /// <summary>
        /// List of files found in the archive
        /// </summary>
        public Dictionary<string, CompressedFile> Files { get; private set; }

        /// <summary>
        /// Stream representing the input archive
        /// </summary>
        private Stream inputStream;

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
        public (byte[] data, string err) Extract(string fullPath)
        {
            // If the file isn't in the archive, we can't extract it
            if (!Exists(fullPath))
                return (null, $"Path '{fullPath}' does not exist in the archive");

            // Get a local reference to the file we care about
            CompressedFile file = Files[fullPath];

            // Attempt to read the compressed data
            inputStream.Seek(DataStart + file.Offset, SeekOrigin.Begin);
            byte[] compressedData = new byte[file.CompressedSize];
            int read = inputStream.Read(compressedData, 0, (int)file.CompressedSize);
            if (read != (int)file.CompressedSize)
                return (null, "Could not read all required data");

            // Decompress the data
            List<byte> output = new List<byte>();
            int ret = BlastDecoder.Blast(compressedData, output);
            if (ret != 0)
                return (null, $"Blast error: {ret}");

            // Return the decompressed data
            return (output.ToArray(), null);
        }

        /// <summary>
        /// Load the file set as the current path
        /// </summary>
        /// <returns>Success and error strings, if applicable</returns>
        private (bool success, string err) LoadFile()
        {
            // If the file doesn't exist, we can't do anything
            if (!File.Exists(FilePath))
                return (false, $"File '{FilePath}' does not exist");

            // Attempt to open the file for reading
            try
            {
                inputStream = File.OpenRead(FilePath);
            }
            catch
            {
                return (false, "Cannot open file");
            }
            
            // Validate that we have at least some data to read
            long fileSize = inputStream.Length;
            if (fileSize <= 51) // sizeof(Header)
                return (false, "File too small");

            // Create the header from the input file
            Header = Header.Create(inputStream);
            if (Header == null)
                return (false, "Header could not be read");

            // Validate the file signature
            if (Header.Signature != 0x8C655D13)
                return (false, "Invalid signature");

            // Validate the TOC
            if (Header.TocAddress >= fileSize)
                return (false, $"Invalid TOC address: {Header.TocAddress}");

            // Move to the TOC
            inputStream.Seek((long)Header.TocAddress, SeekOrigin.Begin);

            // Read all directory info
            for (int i = 0; i < Header.DirCount; i++)
            {
                ushort fileCount = inputStream.ReadUInt16();
                ushort chunkSize = inputStream.ReadUInt16();
                string name      = inputStream.ReadUInt16HeaderedString();

                inputStream.Seek(chunkSize - name.Length - 6, SeekOrigin.Current);
                Directories.Add(new ArchiveDirectory { Name = name, FileCount = fileCount });
            }

            // For each directory, read all file info
            uint accumOffset = 0;
            foreach (ArchiveDirectory directory in Directories)
            {
                for (int i = 0; i < directory.FileCount; i++)
                {
                    // Read in the file information
                    inputStream.Seek(7, SeekOrigin.Current);        // 00-06
                    uint compressedSize = inputStream.ReadUInt32(); // 07-10
                    inputStream.Seek(12, SeekOrigin.Current);       // 11-22
                    ushort chunkSize = inputStream.ReadUInt16();    // 23-24
                    inputStream.Seek(4, SeekOrigin.Current);        // 25-28
                    string filename = inputStream.ReadUInt8HeaderedString();
                    inputStream.Seek(chunkSize - filename.Length - 30, SeekOrigin.Current);

                    // Determine the full path of the internal file
                    string fullpath;
                    if (!string.IsNullOrWhiteSpace(directory.Name) && directory.Name.Length > 0)
                        fullpath = Path.Combine(directory.Name, filename);
                    else
                        fullpath = filename;

                    // Add the file to the list
                    Files[fullpath] = new CompressedFile
                    {
                        Name = filename,
                        FullPath = fullpath,
                        CompressedSize = compressedSize,
                        Offset = accumOffset,
                    };

                    // Accumulate the new offset
                    accumOffset += compressedSize;
                }
            }

            return (true, null);
        }
    }
}