using System;
using System.IO;
using System.Text.RegularExpressions;
using SabreTools.Hashing;
using SabreTools.Compression.zlib;
using SabreTools.Models.InstallShieldCabinet;
using SabreTools.Serialization.Interfaces;
using static SabreTools.Models.InstallShieldCabinet.Constants;

namespace SabreTools.Serialization.Wrappers
{
    public partial class InstallShieldCabinet : WrapperBase<Cabinet>, IExtractable
    {
        #region Descriptive Properties

        /// <inheritdoc/>
        public override string DescriptionString => "InstallShield Cabinet";

        #endregion

        #region Extension Properties

        /// <inheritdoc cref="Cabinet.CommonHeader"/>
        public CommonHeader? CommonHeader => Model.CommonHeader;

        /// <inheritdoc cref="Cabinet.Components"/>
        public Component[]? Components => Model.Components;

        /// <summary>
        /// Number of components in the cabinet set
        /// </summary>
        public int ComponentCount => Components?.Length ?? 0;

        /// <summary>
        /// Number of directories in the cabinet set
        /// </summary>
        public ushort DirectoryCount => Model.Descriptor?.DirectoryCount ?? 0;

        /// <inheritdoc cref="Cabinet.DirectoryNames"/>
        public string[]? DirectoryNames => Model.DirectoryNames;

        /// <summary>
        /// Number of files in the cabinet set
        /// </summary>
        public uint FileCount => Model.Descriptor?.FileCount ?? 0;

        /// <inheritdoc cref="Cabinet.FileDescriptors"/>
        public FileDescriptor[]? FileDescriptors => Model.FileDescriptors;

        /// <inheritdoc cref="Cabinet.FileGroups"/>
        public FileGroup[]? FileGroups => Model.FileGroups;

        /// <summary>
        /// Number of file groups in the cabinet set
        /// </summary>
        public int FileGroupCount => Model.FileGroups?.Length ?? 0;

        /// <summary>
        /// Indicates if Unicode strings are used
        /// </summary>
        public bool IsUnicode => MajorVersion >= 17;

        /// <summary>
        /// The major version of the cabinet
        /// </summary>
        public int MajorVersion => Model.GetMajorVersion();

        /// <inheritdoc cref="Cabinet.VolumeHeader"/>
        public VolumeHeader? VolumeHeader => Model.VolumeHeader;

        /// <summary>
        /// Reference to the next cabinet header
        /// </summary>
        /// <remarks>Only used in multi-file</remarks>
        public InstallShieldCabinet? Next { get; set; }

        /// <summary>
        /// Reference to the next previous header
        /// </summary>
        /// <remarks>Only used in multi-file</remarks>
        public InstallShieldCabinet? Prev { get; set; }

        /// <summary>
        /// Volume index ID, 0 for headers
        /// </summary>
        /// <remarks>Only used in multi-file</remarks>
        public ushort VolumeID { get; set; }

        #endregion

        #region Extraction State

        /// <summary>
        /// Base filename path for related CAB files
        /// </summary>
        internal string? FilenamePattern { get; set; }

        #endregion

        #region Constants

        /// <summary>
        /// Default buffer size
        /// </summary>
        private const int BUFFER_SIZE = 64 * 1024;

        /// <summary>
        /// Maximum size of the window in bits
        /// </summary>
        private const int MAX_WBITS = 15;

        #endregion

        #region Constructors

        /// <inheritdoc/>
        public InstallShieldCabinet(Cabinet? model, byte[]? data, int offset)
            : base(model, data, offset)
        {
            // All logic is handled by the base class
        }

        /// <inheritdoc/>
        public InstallShieldCabinet(Cabinet? model, Stream? data)
            : base(model, data)
        {
            // All logic is handled by the base class
        }

        /// <summary>
        /// Create an InstallShield Cabinet from a byte array and offset
        /// </summary>
        /// <param name="data">Byte array representing the cabinet</param>
        /// <param name="offset">Offset within the array to parse</param>
        /// <returns>A cabinet wrapper on success, null on failure</returns>
        public static InstallShieldCabinet? Create(byte[]? data, int offset)
        {
            // If the data is invalid
            if (data == null || data.Length == 0)
                return null;

            // If the offset is out of bounds
            if (offset < 0 || offset >= data.Length)
                return null;

            // Create a memory stream and use that
            var dataStream = new MemoryStream(data, offset, data.Length - offset);
            return Create(dataStream);
        }

        /// <summary>
        /// Create a InstallShield Cabinet from a Stream
        /// </summary>
        /// <param name="data">Stream representing the cabinet</param>
        /// <returns>A cabinet wrapper on success, null on failure</returns>
        public static InstallShieldCabinet? Create(Stream? data)
        {
            // If the data is invalid
            if (data == null || !data.CanRead)
                return null;

            try
            {
                // Cache the current offset
                long currentOffset = data.Position;

                var model = Deserializers.InstallShieldCabinet.DeserializeStream(data);
                if (model == null)
                    return null;

                data.Seek(currentOffset, SeekOrigin.Begin);
                return new InstallShieldCabinet(model, data);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Cabinet Set

        /// <summary>
        /// Open a cabinet set for reading, if possible
        /// </summary>
        /// <param name="pattern">Filename pattern for matching cabinet files</param>
        /// <returns>Wrapper representing the set, null on error</returns>
        public static InstallShieldCabinet? OpenSet(string? pattern)
        {
            // An invalid pattern means no cabinet files
            if (string.IsNullOrEmpty(pattern))
                return null;

            // Create a placeholder wrapper for output
            InstallShieldCabinet? set = null;

            // Loop until there are no parts left
            bool iterate = true;
            InstallShieldCabinet? previous = null;
            for (ushort i = 1; iterate; i++)
            {
                var file = OpenFileForReading(pattern, i, HEADER_SUFFIX);
                if (file != null)
                    iterate = false;
                else
                    file = OpenFileForReading(pattern, i, CABINET_SUFFIX);

                if (file == null)
                    break;

                var current = Create(file);
                if (current == null)
                    break;

                current.VolumeID = i;
                if (previous != null)
                {
                    previous.Next = current;
                    current.Prev = previous;
                }
                else
                {
                    set = current;
                    previous = current;
                }
            }

            // Set the pattern, if possible
            if (set != null)
                set.FilenamePattern = pattern;

            return set;
        }

        /// <summary>
        /// Open the numbered cabinet set volume
        /// </summary>
        /// <param name="volumeId">Volume ID, 1-indexed</param>
        /// <returns>Wrapper representing the volume on success, null otherwise</returns>
        public InstallShieldCabinet? OpenVolume(ushort volumeId, out Stream? volumeStream)
        {
            // Normalize the volume ID for odd cases
            if (volumeId == ushort.MinValue || volumeId == ushort.MaxValue)
                volumeId = 1;

            // Try to open the file as a stream
            volumeStream = OpenFileForReading(FilenamePattern, volumeId, CABINET_SUFFIX);
            if (volumeStream == null)
            {
                Console.Error.WriteLine($"Failed to open input cabinet file {volumeId}");
                return null;
            }

            // Try to parse the stream into a cabinet
            var volume = Create(volumeStream);
            if (volume == null)
            {
                Console.Error.WriteLine($"Failed to open input cabinet file {volumeId}");
                return null;
            }

            // Set the volume ID and return
            volume.VolumeID = volumeId;
            return volume;
        }

        /// <summary>
        /// Open a cabinet file for reading
        /// </summary>
        /// <param name="index">Cabinet part index to be opened</param>
        /// <param name="suffix">Cabinet files suffix (e.g. `.cab`)</param>
        /// <returns>A Stream representing the cabinet part, null on error</returns>
        public Stream? OpenFileForReading(int index, string suffix)
            => OpenFileForReading(FilenamePattern, index, suffix);

        /// <summary>
        /// Create the generic filename pattern to look for from the input filename
        /// </summary>
        /// <returns>String representing the filename pattern for a cabinet set, null on error</returns>
        private static string? CreateFilenamePattern(string filename)
        {
            string? pattern = null;
            if (string.IsNullOrEmpty(filename))
                return pattern;

            string? directory = Path.GetDirectoryName(Path.GetFullPath(filename));
            if (directory != null)
                pattern = Path.Combine(directory, Path.GetFileNameWithoutExtension(filename));
            else
                pattern = Path.GetFileNameWithoutExtension(filename);

            return new Regex(@"\d+$").Replace(pattern, string.Empty);
        }

        /// <summary>
        /// Open a cabinet file for reading
        /// </summary>
        /// <param name="pattern">Filename pattern for matching cabinet files</param>
        /// <param name="index">Cabinet part index to be opened</param>
        /// <param name="suffix">Cabinet files suffix (e.g. `.cab`)</param>
        /// <returns>A Stream representing the cabinet part, null on error</returns>
        private static Stream? OpenFileForReading(string? pattern, int index, string suffix)
        {
            // An invalid pattern means no cabinet files
            if (string.IsNullOrEmpty(pattern))
                return null;

            // Attempt lower-case extension
            string filename = $"{pattern}{index}.{suffix}";
            if (File.Exists(filename))
                return File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Attempt upper-case extension
            filename = $"{pattern}{index}.{suffix.ToUpperInvariant()}";
            if (File.Exists(filename))
                return File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            return null;
        }

        #endregion

        #region Component

        /// <summary>
        /// Get the component name at a given index, if possible
        /// </summary>
        public string? GetComponentName(int index)
        {
            if (Components == null)
                return null;

            if (index < 0 || index >= ComponentCount)
                return null;

            var component = Components[index];
            if (component?.Identifier == null)
                return null;

            return component.Identifier.Replace('\\', '/');
        }

        #endregion

        #region Directory

        /// <summary>
        /// Get the directory name at a given index, if possible
        /// </summary>
        public string? GetDirectoryName(int index)
        {
            if (DirectoryNames == null)
                return null;

            if (index < 0 || index >= DirectoryNames.Length)
                return null;

            return DirectoryNames[index];
        }

        /// <summary>
        /// Get the directory index for the given file index
        /// </summary>
        /// <returns>Directory index if found, UInt32.MaxValue on error</returns>
        public uint GetDirectoryIndexFromFile(int index)
        {
            FileDescriptor? descriptor = GetFileDescriptor(index);
            if (descriptor != null)
                return descriptor.DirectoryIndex;
            else
                return uint.MaxValue;
        }

        #endregion

        #region Extraction

        /// <inheritdoc/>
        public bool Extract(string outputDirectory, bool includeDebug)
        {
            // Open the full set if possible
            var cabinet = this;
            if (Filename != null)
            {
                // Get the name of the first cabinet file or header
                string pattern = CreateFilenamePattern(Filename)!;
                bool cabinetHeaderExists = File.Exists(pattern + "1.hdr");
                bool shouldScanCabinet = cabinetHeaderExists
                    ? Filename.Equals(pattern + "1.hdr", StringComparison.OrdinalIgnoreCase)
                    : Filename.Equals(pattern + "1.cab", StringComparison.OrdinalIgnoreCase);

                // If we have anything but the first file
                if (!shouldScanCabinet)
                    return false;

                // Open the set from the pattern
                cabinet = OpenSet(pattern);
            }

            // If the cabinet set could not be opened
            if (cabinet == null)
                return false;

            try
            {
                for (int i = 0; i < cabinet.FileCount; i++)
                {
                    try
                    {
                        // Check if the file is valid first
                        if (!cabinet.FileIsValid(i))
                            continue;

                        // Ensure directory separators are consistent
                        string filename = cabinet.GetFileName(i) ?? $"BAD_FILENAME{i}";
                        if (Path.DirectorySeparatorChar == '\\')
                            filename = filename.Replace('/', '\\');
                        else if (Path.DirectorySeparatorChar == '/')
                            filename = filename.Replace('\\', '/');

                        // Ensure the full output directory exists
                        filename = Path.Combine(outputDirectory, filename);
                        var directoryName = Path.GetDirectoryName(filename);
                        if (directoryName != null && !Directory.Exists(directoryName))
                            Directory.CreateDirectory(directoryName);

                        cabinet.FileSave(i, filename);
                    }
                    catch (Exception ex)
                    {
                        if (includeDebug) Console.Error.WriteLine(ex);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                if (includeDebug) Console.Error.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// Save the file at the given index to the filename specified
        /// </summary>
        public bool FileSave(int index, string filename, bool useOld = false)
        {
            // Get the file descriptor
            if (!TryGetFileDescriptor(index, out var fileDescriptor) || fileDescriptor == null)
                return false;

            // If the file is split
            if (fileDescriptor.LinkFlags == LinkFlags.LINK_PREV)
                return FileSave((int)fileDescriptor.LinkPrevious, filename, useOld);

            // Get the reader at the index
            var reader = Reader.Create(this, index, fileDescriptor);
            if (reader == null)
                return false;

            // Create the output file and hasher
            FileStream output = File.OpenWrite(filename);
            var md5 = new HashWrapper(HashType.MD5);

            long readBytesLeft = (long)GetReadableBytes(fileDescriptor);
            long writeBytesLeft = (long)GetWritableBytes(fileDescriptor);
            byte[] inputBuffer;
            byte[] outputBuffer = new byte[BUFFER_SIZE];
            long totalWritten = 0;

            // Read while there are bytes remaining
            while (readBytesLeft > 0 && writeBytesLeft > 0)
            {
                long bytesToWrite = BUFFER_SIZE;
                int result;

                // Handle compressed files
#if NET20 || NET35
                if ((fileDescriptor.Flags & FileFlags.FILE_COMPRESSED) != 0)
#else
                if (fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
#endif
                {
                    // Attempt to read the length value
                    byte[] lengthArr = new byte[sizeof(ushort)];
                    if (!reader.Read(lengthArr, 0, lengthArr.Length))
                    {
                        Console.Error.WriteLine($"Failed to read {lengthArr.Length} bytes of file {index} ({GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Attempt to read the specified number of bytes
                    ushort bytesToRead = BitConverter.ToUInt16(lengthArr, 0);
                    inputBuffer = new byte[BUFFER_SIZE + 1];
                    if (!reader.Read(inputBuffer, 0, bytesToRead))
                    {
                        Console.Error.WriteLine($"Failed to read {lengthArr.Length} bytes of file {index} ({GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Add a null byte to make inflate happy
                    inputBuffer[bytesToRead] = 0;
                    ulong readBytes = (ulong)(bytesToRead + 1);

                    // Uncompress into a buffer
                    if (useOld)
                        result = UncompressOld(outputBuffer, ref bytesToWrite, inputBuffer, ref readBytes);
                    else
                        result = Uncompress(outputBuffer, ref bytesToWrite, inputBuffer, ref readBytes);

                    // If we didn't get a positive result that's not a data error (false positives)
                    if (result != zlibConst.Z_OK && result != zlibConst.Z_DATA_ERROR)
                    {
                        Console.Error.WriteLine($"Decompression failed with code {result.ToZlibConstName()}. bytes_to_read={bytesToRead}, volume={fileDescriptor.Volume}, read_bytes={readBytes}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Set remaining bytes
                    readBytesLeft -= 2;
                    readBytesLeft -= bytesToRead;
                }

                // Handle uncompressed files
                else
                {
                    bytesToWrite = Math.Min(readBytesLeft, BUFFER_SIZE);
                    if (!reader.Read(outputBuffer, 0, (int)bytesToWrite))
                    {
                        Console.Error.WriteLine($"Failed to write {bytesToWrite} bytes from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Set remaining bytes
                    readBytesLeft -= (uint)bytesToWrite;
                }

                // Hash and write the next block
                bytesToWrite = Math.Min(bytesToWrite, writeBytesLeft);
                md5.Process(outputBuffer, 0, (int)bytesToWrite);
                output?.Write(outputBuffer, 0, (int)bytesToWrite);

                totalWritten += bytesToWrite;
                writeBytesLeft -= bytesToWrite;
            }

            // Validate the number of bytes written
            if ((long)fileDescriptor.ExpandedSize != totalWritten)
                Console.WriteLine($"Expanded size of file {index} ({GetFileName(index)}) expected to be {fileDescriptor.ExpandedSize}, but was {totalWritten}");

            // Finalize output values
            md5.Terminate();
            reader?.Dispose();
            output?.Close();

            // Validate the data written, if required
            if (MajorVersion >= 6)
            {
                string expectedMd5 = BitConverter.ToString(fileDescriptor.MD5!);
                expectedMd5 = expectedMd5.ToLowerInvariant().Replace("-", string.Empty);

                string? actualMd5 = md5.CurrentHashString;
                if (actualMd5 == null || actualMd5 != expectedMd5)
                {
                    Console.Error.WriteLine($"MD5 checksum failure for file {index} ({GetFileName(index)})");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Save the file at the given index to the filename specified as raw
        /// </summary>
        public bool FileSaveRaw(int index, string filename)
        {
            // Get the file descriptor
            if (!TryGetFileDescriptor(index, out var fileDescriptor) || fileDescriptor == null)
                return false;

            // If the file is split
            if (fileDescriptor.LinkFlags == LinkFlags.LINK_PREV)
                return FileSaveRaw((int)fileDescriptor.LinkPrevious, filename);

            // Get the reader at the index
            var reader = Reader.Create(this, index, fileDescriptor);
            if (reader == null)
                return false;

            // Create the output file
            FileStream output = File.OpenWrite(filename);

            ulong bytesLeft = GetReadableBytes(fileDescriptor);
            byte[] outputBuffer = new byte[BUFFER_SIZE];

            // Read while there are bytes remaining
            while (bytesLeft > 0)
            {
                ulong bytesToWrite = Math.Min(bytesLeft, BUFFER_SIZE);
                if (!reader.Read(outputBuffer, 0, (int)bytesToWrite))
                {
                    Console.Error.WriteLine($"Failed to read {bytesToWrite} bytes from input cabinet file {fileDescriptor.Volume}");
                    reader.Dispose();
                    output?.Close();
                    return false;
                }

                // Set remaining bytes
                bytesLeft -= (uint)bytesToWrite;

                // Write the next block
                output.Write(outputBuffer, 0, (int)bytesToWrite);
            }

            // Finalize output values
            reader.Dispose();
            output?.Close();
            return true;
        }

        /// <summary>
        /// Uncompress a source byte array to a destination
        /// </summary>
        private unsafe static int Uncompress(byte[] dest, ref long destLen, byte[] source, ref ulong sourceLen)
        {
            fixed (byte* sourcePtr = source)
            fixed (byte* destPtr = dest)
            {
                var stream = new ZLib.z_stream_s
                {
                    next_in = sourcePtr,
                    avail_in = (uint)sourceLen,
                    next_out = destPtr,
                    avail_out = (uint)destLen,
                };

                // make second parameter negative to disable checksum verification
                int err = ZLib.inflateInit2_(stream, -MAX_WBITS, ZLib.zlibVersion(), source.Length);
                if (err != zlibConst.Z_OK)
                    return err;

                err = ZLib.inflate(stream, 1);
                if (err != zlibConst.Z_OK && err != zlibConst.Z_STREAM_END)
                {
                    ZLib.inflateEnd(stream);
                    return err;
                }

                destLen = stream.total_out;
                sourceLen = stream.total_in;
                return ZLib.inflateEnd(stream);
            }
        }

        /// <summary>
        /// Uncompress a source byte array to a destination (old version)
        /// </summary>
        private unsafe static int UncompressOld(byte[] dest, ref long destLen, byte[] source, ref ulong sourceLen)
        {
            fixed (byte* sourcePtr = source)
            fixed (byte* destPtr = dest)
            {
                var stream = new ZLib.z_stream_s
                {
                    next_in = sourcePtr,
                    avail_in = (uint)sourceLen,
                    next_out = destPtr,
                    avail_out = (uint)destLen,
                };

                destLen = 0;
                sourceLen = 0;

                // make second parameter negative to disable checksum verification
                int err = ZLib.inflateInit2_(stream, -MAX_WBITS, ZLib.zlibVersion(), source.Length);
                if (err != zlibConst.Z_OK)
                    return err;

                while (stream.avail_in > 1)
                {
                    err = ZLib.inflate(stream, 1);
                    if (err != zlibConst.Z_OK)
                    {
                        ZLib.inflateEnd(stream);
                        return err;
                    }
                }

                destLen = stream.total_out;
                sourceLen = stream.total_in;
                return ZLib.inflateEnd(stream);
            }
        }

        #endregion

        #region File

        /// <summary>
        /// Returns if the file at a given index is marked as valid
        /// </summary>
        public bool FileIsValid(int index)
        {
            if (index < 0 || index > FileCount)
                return false;

            FileDescriptor? descriptor = GetFileDescriptor(index);
            if (descriptor == null)
                return false;

            if (descriptor.IsInvalid())
                return false;

            if (descriptor.NameOffset == default)
                return false;

            if (descriptor.DataOffset == default)
                return false;

            return true;
        }

        /// <summary>
        /// Get the reported expanded file size for a given index
        /// </summary>
        public ulong GetExpandedFileSize(int index)
        {
            FileDescriptor? descriptor = GetFileDescriptor(index);
            if (descriptor != null)
                return descriptor.ExpandedSize;
            else
                return 0;
        }

        /// <summary>
        /// Get the file descriptor at a given index, if possible
        /// </summary>
        public FileDescriptor? GetFileDescriptor(int index)
        {
            if (FileDescriptors == null)
                return null;

            if (index < 0 || index >= FileDescriptors.Length)
                return null;

            return FileDescriptors[index];
        }

        /// <summary>
        /// Get the file descriptor at a given index, if possible
        /// </summary>
        /// <remarks>Verifies the file descriptor flags before returning</remarks>
        public bool TryGetFileDescriptor(int index, out FileDescriptor? fileDescriptor)
        {
            fileDescriptor = GetFileDescriptor(index);
            if (fileDescriptor == null)
            {
                Console.Error.WriteLine($"Failed to get file descriptor for file {index}");
                return false;
            }

            if (fileDescriptor.IsInvalid() || fileDescriptor.DataOffset == 0)
            {
                Console.Error.WriteLine($"File at {index} is marked as invalid");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the file name at a given index, if possible
        /// </summary>
        public string? GetFileName(int index)
        {
            var descriptor = GetFileDescriptor(index);
            if (descriptor == null || descriptor.IsInvalid())
                return null;

            return descriptor.Name;
        }

        /// <summary>
        /// Get the packed size of a file, if possible
        /// </summary>
        public static ulong GetReadableBytes(FileDescriptor? descriptor)
        {
            if (descriptor == null)
                return 0;

            return descriptor.IsCompressed()
                ? descriptor.CompressedSize
                : descriptor.ExpandedSize;
        }

        /// <summary>
        /// Get the packed size of a file, if possible
        /// </summary>
        public static ulong GetWritableBytes(FileDescriptor? descriptor)
        {
            if (descriptor == null)
                return 0;

            return descriptor.ExpandedSize;
        }

        #endregion

        #region File Group

        /// <summary>
        /// Get the file group at a given index, if possible
        /// </summary>
        public FileGroup? GetFileGroup(int index)
        {
            if (FileGroups == null)
                return null;

            if (index < 0 || index >= FileGroups.Length)
                return null;

            return FileGroups[index];
        }

        /// <summary>
        /// Get the file group at a given name, if possible
        /// </summary>
        public FileGroup? GetFileGroup(string name)
        {
            if (FileGroups == null)
                return null;

            return Array.Find(FileGroups, fg => fg != null && string.Equals(fg.Name, name));
        }

        /// <summary>
        /// Get the file group for the given file index, if possible
        /// </summary>
        public FileGroup? GetFileGroupFromFile(int index)
        {
            if (FileGroups == null)
                return null;

            if (index < 0 || index >= FileCount)
                return null;

            for (int i = 0; i < FileGroupCount; i++)
            {
                var fileGroup = GetFileGroup(i);
                if (fileGroup == null)
                    continue;

                if (fileGroup.FirstFile > index || fileGroup.LastFile < index)
                    continue;

                return fileGroup;
            }

            return null;
        }

        /// <summary>
        /// Get the file group name at a given index, if possible
        /// </summary>
        public string? GetFileGroupName(int index)
            => GetFileGroup(index)?.Name;

        /// <summary>
        /// Get the file group name at a given file index, if possible
        /// </summary>
        public string? GetFileGroupNameFromFile(int index)
            => GetFileGroupFromFile(index)?.Name;

        #endregion

        #region Obfuscation

        /// <summary>
        /// Deobfuscate a buffer
        /// </summary>
        public static void Deobfuscate(byte[] buffer, long size, ref uint offset)
        {
            offset = Deobfuscate(buffer, size, offset);
        }

        /// <summary>
        /// Deobfuscate a buffer with a seed value
        /// </summary>
        /// <remarks>Seed is 0 at file start</remarks>
        public static uint Deobfuscate(byte[] buffer, long size, uint seed)
        {
            for (int i = 0; size > 0; size--, i++, seed++)
            {
                buffer[i] = (byte)(ROR8(buffer[i] ^ 0xd5, 2) - (seed % 0x47));
            }

            return seed;
        }

        /// <summary>
        /// Obfuscate a buffer
        /// </summary>
        public static void Obfuscate(byte[] buffer, long size, ref uint offset)
        {
            offset = Obfuscate(buffer, size, offset);
        }

        /// <summary>
        /// Obfuscate a buffer with a seed value
        /// </summary>
        /// <remarks>Seed is 0 at file start</remarks>
        public static uint Obfuscate(byte[] buffer, long size, uint seed)
        {
            for (int i = 0; size > 0; size--, i++, seed++)
            {
                buffer[i] = (byte)(ROL8(buffer[i] ^ 0xd5, 2) + (seed % 0x47));
            }

            return seed;
        }

        /// <summary>
        /// Rotate Right 8
        /// </summary>
        private static int ROR8(int x, byte n) => (x >> n) | (x << (8 - n));

        /// <summary>
        /// Rotate Left 8
        /// </summary>
        private static int ROL8(int x, byte n) => (x << n) | (x >> (8 - n));

        #endregion

        #region Helper Classes

        /// <summary>
        /// Helper to read a single file from a cabinet set
        /// </summary>
        private class Reader : IDisposable
        {
            #region Private Instance Variables

            /// <summary>
            /// Cabinet file to read from
            /// </summary>
            private readonly InstallShieldCabinet _cabinet;

            /// <summary>
            /// Currently selected index
            /// </summary>
            private readonly uint _index;

            /// <summary>
            /// File descriptor defining the currently selected index
            /// </summary>
            private readonly FileDescriptor _fileDescriptor;

            /// <summary>
            /// Offset in the data where the file exists
            /// </summary>
            private ulong _dataOffset;

            /// <summary>
            /// Number of bytes left in the current volume
            /// </summary>
            private ulong _volumeBytesLeft;

            /// <summary>
            /// Handle to the current volume stream
            /// </summary>
            private Stream? _volumeFile;

            /// <summary>
            /// Current volume header
            /// </summary>
            private VolumeHeader? _volumeHeader;

            /// <summary>
            /// Current volume ID
            /// </summary>
            private ushort _volumeId;

            /// <summary>
            /// Offset for obfuscation seed
            /// </summary>
            private uint _obfuscationOffset;

            #endregion

            #region Constructors

            private Reader(InstallShieldCabinet cabinet, uint index, FileDescriptor fileDescriptor)
            {
                _cabinet = cabinet;
                _index = index;
                _fileDescriptor = fileDescriptor;
            }

            #endregion

            /// <summary>
            /// Create a new <see cref="Reader"> from an existing cabinet, index, and file descriptor
            /// </summary>
            public static Reader? Create(InstallShieldCabinet cabinet, int index, FileDescriptor fileDescriptor)
            {
                var reader = new Reader(cabinet, (uint)index, fileDescriptor);
                for (; ; )
                {
                    // If the volume is invalid
                    if (!reader.OpenVolume(fileDescriptor.Volume))
                    {
                        Console.Error.WriteLine($"Failed to open volume {fileDescriptor.Volume}");
                        return null;
                    }
                    else if (reader._volumeFile == null || reader._volumeHeader == null)
                    {
                        Console.Error.WriteLine($"Volume {fileDescriptor.Volume} is invalid");
                        return null;
                    }

                    // Start with the correct volume for IS5 cabinets
                    if (reader._cabinet.MajorVersion <= 5 && index > (int)reader._volumeHeader.LastFileIndex)
                    {
                        // Normalize the volume ID for odd cases
                        if (fileDescriptor.Volume == ushort.MinValue || fileDescriptor.Volume == ushort.MaxValue)
                            fileDescriptor.Volume = 1;

                        fileDescriptor.Volume++;
                        continue;
                    }

                    break;
                }

                return reader;
            }

            /// <summary>
            /// Dispose of the current object
            /// </summary>
            public void Dispose()
            {
                _volumeFile?.Close();
            }

            #region Reading

            /// <summary>
            /// Read a certain number of bytes from the current volume
            /// </summary>
            public bool Read(byte[] buffer, int start, long size)
            {
                long bytesLeft = size;
                while (bytesLeft > 0)
                {
                    // Open the next volume, if necessary
                    if (_volumeBytesLeft == 0)
                    {
                        if (!OpenNextVolume(out _))
                            return false;
                    }

                    // Get the number of bytes to read from this volume
                    int bytesToRead = (int)Math.Min(bytesLeft, (long)_volumeBytesLeft);
                    if (bytesToRead == 0)
                        break;

                    // Read as much as possible from this volume
                    if (bytesToRead != _volumeFile!.Read(buffer, start, bytesToRead))
                        return false;

                    // Set the number of bytes left
                    bytesLeft -= bytesToRead;
                    _volumeBytesLeft -= (uint)bytesToRead;
                }

#if NET20 || NET35
                if ((_fileDescriptor.Flags & FileFlags.FILE_OBFUSCATED) != 0)
#else
                if (_fileDescriptor.Flags.HasFlag(FileFlags.FILE_OBFUSCATED))
#endif
                    Deobfuscate(buffer, size, ref _obfuscationOffset);

                return true;
            }

            /// <summary>
            /// Open the next volume based on the current index
            /// </summary>
            private bool OpenNextVolume(out ushort nextVolume)
            {
                nextVolume = (ushort)(_volumeId + 1);
                return OpenVolume(nextVolume);
            }

            /// <summary>
            /// Open the volume at the inputted index
            /// </summary>
            private bool OpenVolume(ushort volume)
            {
                // Read the volume from the cabinet set
                var next = _cabinet.OpenVolume(volume, out var volumeStream);
                if (next?.VolumeHeader == null || volumeStream == null)
                {
                    Console.Error.WriteLine($"Failed to open input cabinet file {volume}");
                    return false;
                }

                // Assign the next items
                _volumeFile?.Close();
                _volumeFile = volumeStream;
                _volumeHeader = next.VolumeHeader;

                // Enable support for split archives for IS5
                if (_cabinet.MajorVersion == 5)
                {
                    if (_index < (_cabinet.FileCount - 1)
                        && _index == _volumeHeader.LastFileIndex
                        && _volumeHeader.LastFileSizeCompressed != _fileDescriptor.CompressedSize)
                    {
                        _fileDescriptor.Flags |= FileFlags.FILE_SPLIT;
                    }
                    else if (_index > 0
                        && _index == _volumeHeader.FirstFileIndex
                        && _volumeHeader.FirstFileSizeCompressed != _fileDescriptor.CompressedSize)
                    {
                        _fileDescriptor.Flags |= FileFlags.FILE_SPLIT;
                    }
                }

                ulong volumeBytesLeftCompressed, volumeBytesLeftExpanded;
#if NET20 || NET35
            if ((_fileDescriptor.Flags & FileFlags.FILE_SPLIT) != 0)
#else
                if (_fileDescriptor.Flags.HasFlag(FileFlags.FILE_SPLIT))
#endif
                {
                    if (_index == _volumeHeader.LastFileIndex && _volumeHeader.LastFileOffset != 0x7FFFFFFF)
                    {
                        // can be first file too
                        _dataOffset = _volumeHeader.LastFileOffset;
                        volumeBytesLeftExpanded = _volumeHeader.LastFileSizeExpanded;
                        volumeBytesLeftCompressed = _volumeHeader.LastFileSizeCompressed;
                    }
                    else if (_index == _volumeHeader.FirstFileIndex)
                    {
                        _dataOffset = _volumeHeader.FirstFileOffset;
                        volumeBytesLeftExpanded = _volumeHeader.FirstFileSizeExpanded;
                        volumeBytesLeftCompressed = _volumeHeader.FirstFileSizeCompressed;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    _dataOffset = _fileDescriptor.DataOffset;
                    volumeBytesLeftExpanded = _fileDescriptor.ExpandedSize;
                    volumeBytesLeftCompressed = _fileDescriptor.CompressedSize;
                }

#if NET20 || NET35
                if ((_fileDescriptor.Flags & FileFlags.FILE_COMPRESSED) != 0)
#else
                if (_fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
#endif
                    _volumeBytesLeft = volumeBytesLeftCompressed;
                else
                    _volumeBytesLeft = volumeBytesLeftExpanded;

                _volumeFile.Seek((long)_dataOffset, SeekOrigin.Begin);
                _volumeId = volume;

                return true;
            }

            #endregion
        }

        #endregion
    }
}
