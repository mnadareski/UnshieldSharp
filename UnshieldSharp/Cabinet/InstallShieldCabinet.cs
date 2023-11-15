using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ComponentAce.Compression.Libs.zlib;
using SabreTools.Models.InstallShieldCabinet;
using static UnshieldSharp.Cabinet.Constants;

namespace UnshieldSharp.Cabinet
{
    // TODO: Figure out if individual parts of a split cab can be extracted separately
    public class InstallShieldCabinet
    {
        // Linked CAB headers
        public Header? HeaderList { get; private set; }

        // Internal CAB Counts
        public int ComponentCount => HeaderList?.ComponentCount ?? 0;
        public int DirectoryCount => (int)(HeaderList?.DirectoryCount ?? 0); // TODO: multi-volume support...
        public int FileCount => (int)(HeaderList?.FileCount ?? 0); // TODO: multi-volume support...
        public int FileGroupCount => HeaderList?.FileGroupCount ?? 0;

        // Unicode compatibility
        public bool IsUnicode => HeaderList?.MajorVersion >= 17;

        // Base filename path for related CAB files
        private string? filenamePattern;

        #region Open Cabinet

        /// <summary>
        /// Open a file as an InstallShield CAB
        /// </summary>
        public static InstallShieldCabinet? Open(string filename)
        {
            var cabinet = new InstallShieldCabinet();
            if (!cabinet.CreateFilenamePattern(filename))
            {
                Console.Error.WriteLine("Failed to create filename pattern");
                return null;
            }

            if (!cabinet.ReadHeaders())
            {
                Console.Error.WriteLine("Failed to read header files");
                return null;
            }

            return cabinet;
        }

        #endregion

        #region Name From Index

        /// <summary>
        /// Get the component name at an index
        /// </summary>
        public string? ComponentName(int index) => HeaderList?.GetComponentName(index);

        /// <summary>
        /// Get the directory name at an index
        /// </summary>
        public string? DirectoryName(int index) => HeaderList?.GetDirectoryName(index);

        /// <summary>
        /// Get the file name at an index
        /// </summary>
        public string? FileName(int index) => HeaderList?.GetFileName(index);

        /// <summary>
        /// Get the file group name at an index
        /// </summary>
        public string? FileGroupName(int index) => HeaderList?.GetFileGroupName(index);

        #endregion

        #region File

        /// <summary>
        /// Returns if the file at a given index is marked as valid
        /// </summary>
        public bool FileIsValid(int index)
        {
            if (index < 0 || index > FileCount)
                return false;

            FileDescriptor? fd = HeaderList?.GetFileDescriptor(index);
            if (fd == null)
                return false;

            if (fd.Flags.HasFlag(FileFlags.FILE_INVALID))
                return false;

            if (fd.NameOffset == default)
                return false;

            if (fd.DataOffset == default)
                return false;

            return true;
        }

        /// <summary>
        /// Save the file at the given index to the filename specified
        /// </summary>
        public bool FileSave(int index, string filename)
        {
            var fileDescriptor = GetFileDescriptor(filename, index);
            if (fileDescriptor == null)
                return false;

            if (fileDescriptor.LinkFlags == LinkFlags.LINK_PREV)
                return FileSave((int)fileDescriptor.LinkPrevious, filename);

            var reader = GetReader(index, fileDescriptor);
            if (reader == null)
                return false;

            var output = File.OpenWrite(filename);
            if (output == null)
            {
                Console.Error.WriteLine($"Failed to open {filename} for writing");
                return false;
            }

            MD5 md5 = MD5.Create();
            md5.Initialize();
            ulong bytesLeft = GetBytesToRead(fileDescriptor);
            byte[] inputBuffer;
            byte[] outputBuffer = new byte[BUFFER_SIZE];
            ulong totalWritten = 0;
            while (bytesLeft > 0)
            {
                ulong bytesToWrite = BUFFER_SIZE;
                int result;

                if (fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
                {
                    ulong readBytes;
                    byte[] bytesToRead = new byte[sizeof(ushort)];

                    // Attempt to read the length value
                    if (!reader.Read(bytesToRead, 0, bytesToRead.Length))
                    {
                        Console.Error.WriteLine($"Failed to read {bytesToRead.Length} bytes of file {index} ({FileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Validate the number of bytes to read
                    ushort bytesToReadValue = BitConverter.ToUInt16(bytesToRead, 0);
                    if (bytesToReadValue == 0)
                    {
                        Console.Error.WriteLine("bytesToRead can't be zero");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Attempt to read the specified number of bytes
                    inputBuffer = new byte[BUFFER_SIZE + 1];
                    if (!reader.Read(inputBuffer, 0, bytesToReadValue))
                    {
                        Console.Error.WriteLine($"Failed to read {bytesToRead.Length} bytes of file {index} ({FileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Add a null byte to make inflate happy
                    inputBuffer[bytesToReadValue] = 0;
                    readBytes = (ulong)(bytesToReadValue + 1);

                    // Uncompress into a buffer
                    result = Uncompress(outputBuffer, ref bytesToWrite, inputBuffer, ref readBytes);

                    // If we didn't get a positive result that's not a data error (false positives)
                    if (result != zlibConst.Z_OK && result != zlibConst.Z_DATA_ERROR)
                    {
                        Console.Error.WriteLine($"Decompression failed with code {result.ToZlibConstName()}. bytes_to_read={bytesToReadValue}, volume_bytes_left={reader.VolumeBytesLeft}, volume={fileDescriptor.Volume}, read_bytes={readBytes}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    bytesLeft -= 2;
                    bytesLeft -= bytesToReadValue;
                }
                else
                {
                    bytesToWrite = Math.Min(bytesLeft, BUFFER_SIZE);
                    if (!reader.Read(outputBuffer, 0, (int)bytesToWrite))
                    {
                        Console.Error.WriteLine($"Failed to write {bytesToWrite} bytes from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    bytesLeft -= (uint)bytesToWrite;
                }

                md5.TransformBlock(outputBuffer, 0, (int)bytesToWrite, outputBuffer, 0);

                output?.Write(outputBuffer, 0, (int)bytesToWrite);

                totalWritten += bytesToWrite;
            }

            if (fileDescriptor.ExpandedSize != totalWritten)
            {
                Console.Error.WriteLine($"Expanded size expected to be {fileDescriptor.ExpandedSize}, but was {totalWritten}");
                reader.Dispose();
                output?.Close();
                return false;
            }

            if (HeaderList!.MajorVersion >= 6)
            {
                md5.TransformFinalBlock(outputBuffer, 0, 0);
                byte[]? md5result = md5.Hash;

                if (md5result == null || !md5result.SequenceEqual(fileDescriptor.MD5!))
                {
                    Console.Error.WriteLine($"MD5 checksum failure for file {index} ({FileName(index)})");
                    reader.Dispose();
                    output?.Close();
                    return false;
                }
            }

            reader?.Dispose();
            output?.Close();
            return true;
        }

        /// <summary>
        /// Save the file at the given index to the filename specified (old version)
        /// </summary>
        public bool FileSaveOld(int index, string filename)
        {
            var fileDescriptor = GetFileDescriptor(filename, index);
            if (fileDescriptor == null)
                return false;

            if (fileDescriptor.LinkFlags == LinkFlags.LINK_PREV)
                return FileSaveRaw((int)fileDescriptor.LinkPrevious, filename);

            var reader = GetReader(index, fileDescriptor);
            if (reader == null)
                return false;

            var output = File.OpenWrite(filename);
            if (output == null)
            {
                Console.Error.WriteLine($"Failed to open {filename} for writing");
                return false;
            }

            ulong bytesLeft = InstallShieldCabinet.GetBytesToRead(fileDescriptor);
            long inputBufferSize = BUFFER_SIZE;
            byte[] inputBuffer = new byte[BUFFER_SIZE];
            byte[] outputBuffer = new byte[BUFFER_SIZE];
            ulong totalWritten = 0;
            while (bytesLeft > 0)
            {
                ulong bytesToWrite = 0;
                int result;

                if (reader.VolumeBytesLeft == 0 && !reader.OpenVolume(reader.Volume + 1))
                {
                    Console.Error.WriteLine($"Failed to open volume {reader.Volume + 1} to read {bytesLeft} more bytes");
                    reader.Dispose();
                    output?.Close();
                    return false;
                }

                if (fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
                {
                    byte[] END_OF_CHUNK = [0x00, 0x00, 0xff, 0xff];
                    ulong readBytes;
                    long inputSize = (long)reader.VolumeBytesLeft;

                    while (inputSize > inputBufferSize)
                    {
                        inputBufferSize *= 2;
                        Array.Resize(ref inputBuffer, (int)inputBufferSize);
                    }

                    if (!reader.Read(inputBuffer, 0, (int)inputSize))
                    {
                        Console.Error.WriteLine($"Failed to read {inputSize} bytes of file {index} ({FileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    bytesLeft -= (uint)inputSize;
                    for (int p = 0; inputSize > 0;)
                    {
                        int match = InstallShieldCabinet.FindBytes(inputBuffer, p, inputSize, END_OF_CHUNK);
                        if (match == -1)
                        {
                            Console.Error.WriteLine($"Could not find end of chunk for file {index} ({FileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                            reader.Dispose();
                            output?.Close();
                            return false;
                        }

                        long chunkSize = match - p;

                        /*
                        Detect when the chunk actually contains the end of chunk marker.

                        Needed by Qtime.smk from "The Feeble Files - spanish version".
           
                        The first bit of a compressed block is always zero, so we apply this
                        workaround if it's a one.

                        A possibly more proper fix for this would be to have
                        unshield_uncompress_old eat compressed data and discard chunk
                        markers inbetween.
                        */

                        while ((chunkSize + END_OF_CHUNK.Length) < inputSize && (inputBuffer[chunkSize + END_OF_CHUNK.Length] & 1) != 0)
                        {
                            Console.Error.WriteLine("It seems like we have an end of chunk marker inside of a chunk.");
                            chunkSize += END_OF_CHUNK.Length;
                            match = InstallShieldCabinet.FindBytes(inputBuffer, (int)(p + chunkSize), inputSize - chunkSize, END_OF_CHUNK);
                            if (match == -1)
                            {
                                Console.Error.WriteLine($"Could not find end of chunk for file {index} ({FileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                                reader.Dispose();
                                output?.Close();
                                return false;
                            }

                            chunkSize = match - p;
                        }

                        // add a null byte to make inflate happy
                        inputBuffer[chunkSize] = 0;

                        bytesToWrite = BUFFER_SIZE;
                        readBytes = (ulong)chunkSize;
                        result = UncompressOld(outputBuffer, ref bytesToWrite, inputBuffer, ref readBytes);

                        if (result != zlibConst.Z_OK)
                        {
                            Console.Error.WriteLine($"Decompression failed with code {result.ToZlibConstName()}. input_size={inputSize}, volume_bytes_left={reader.VolumeBytesLeft}, volume={fileDescriptor.Volume}, read_bytes={readBytes}");
                            reader.Dispose();
                            output?.Close();
                            return false;
                        }

                        p += (int)chunkSize;
                        p += END_OF_CHUNK.Length;

                        inputSize -= chunkSize;
                        inputSize -= END_OF_CHUNK.Length;

                        output?.Write(outputBuffer, 0, (int)bytesToWrite);

                        totalWritten += bytesToWrite;
                    }
                }
                else
                {
                    bytesToWrite = Math.Min(bytesLeft, BUFFER_SIZE);
                    if (!reader.Read(outputBuffer, 0, (int)bytesToWrite))
                    {
                        Console.Error.WriteLine($"Failed to read {bytesToWrite} bytes from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    bytesLeft -= (uint)bytesToWrite;

                    output?.Write(outputBuffer, 0, (int)bytesToWrite);

                    totalWritten += bytesToWrite;
                }
            }

            if (fileDescriptor.ExpandedSize != totalWritten)
            {
                Console.Error.WriteLine($"Expanded size expected to be {fileDescriptor.ExpandedSize}, but was {totalWritten}");
                reader.Dispose();
                output?.Close();
                return false;
            }

            reader.Dispose();
            output?.Close();
            return true;
        }

        /// <summary>
        /// Save the file at the given index to the filename specified as raw
        /// </summary>
        public bool FileSaveRaw(int index, string filename)
        {
            var fileDescriptor = GetFileDescriptor(filename, index);
            if (fileDescriptor == null)
                return false;

            if (fileDescriptor.LinkFlags == LinkFlags.LINK_PREV)
                return FileSaveRaw((int)fileDescriptor.LinkPrevious, filename);

            var reader = GetReader(index, fileDescriptor);
            if (reader == null)
                return false;

            var output = File.OpenWrite(filename);
            if (output == null)
            {
                Console.Error.WriteLine($"Failed to open {filename} for writing");
                return false;
            }

            ulong bytesLeft = InstallShieldCabinet.GetBytesToRead(fileDescriptor);
            byte[] outputBuffer = new byte[BUFFER_SIZE];
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

                bytesLeft -= (uint)bytesToWrite;
                output.Write(outputBuffer, 0, (int)bytesToWrite);
            }

            reader.Dispose();
            output?.Close();
            return true;
        }

        /// <summary>
        /// Get the directory index for the given file index
        /// </summary>
        public int FileDirectory(int index)
        {
            FileDescriptor? fd = HeaderList?.GetFileDescriptor(index);
            if (fd != null)
                return (int)fd.DirectoryIndex;
            else
                return -1;
        }

        /// <summary>
        /// Get the reported expanded file size for a given index
        /// </summary>
        public int FileSize(int index)
        {
            FileDescriptor? fd = HeaderList?.GetFileDescriptor(index);
            if (fd != null)
                return (int)fd.ExpandedSize;
            else
                return 0;
        }

        /// <summary>
        /// Common code for getting the bytes to read
        /// </summary>
        private static ulong GetBytesToRead(FileDescriptor fd)
        {
            if (fd.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
                return fd.CompressedSize;
            else
                return fd.ExpandedSize;
        }

        /// <summary>
        /// Common code for getting the file descriptor
        /// </summary>
        private FileDescriptor? GetFileDescriptor(string filename, int index)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                Console.Error.WriteLine("Provided filename is invalid");
                return null;
            }

            var fileDescriptor = HeaderList?.GetFileDescriptor(index);
            if (fileDescriptor == null)
            {
                Console.Error.WriteLine($"Failed to get file descriptor for file {index}");
                return null;
            }

            if (fileDescriptor.Flags.HasFlag(FileFlags.FILE_INVALID) || fileDescriptor.DataOffset == 0)
            {
                Console.Error.WriteLine($"File at {index} is marked as invalid");
                return null;
            }

            return fileDescriptor;
        }

        /// <summary>
        /// Common code for getting the reader
        /// </summary>
        private Reader? GetReader(int index, FileDescriptor fd)
        {
            var reader = Reader.Create(this, index, fd);
            if (reader == null)
            {
                Console.Error.WriteLine($"Failed to create data reader for file {index}");
                return null;
            }

            if (reader.VolumeFile!.Length == (long)fd.DataOffset)
            {
                Console.Error.WriteLine($"File {index} is not inside the cabinet.");
                reader.Dispose();
                return null;
            }

            return reader;
        }

        #endregion

        #region File Group

        /// <summary>
        /// Retrieve a file group based on index
        /// </summary>
        public FileGroup? GetFileGroup(int index) => HeaderList?.GetFileGroup(index);

        /// <summary>
        /// Retrieve a file group based on name
        /// </summary>
        public FileGroup? GetFileGroup(string name) => HeaderList?.GetFileGroup(name);

        #endregion

        #region Uncompression

        /// <summary>
        /// Uncompress a source byte array to a destination
        /// </summary>
        public static int Uncompress(byte[] dest, ref ulong destLen, byte[] source, ref ulong sourceLen)
        {
            var stream = new ZStream
            {
                next_in = source,
                avail_in = (int)sourceLen,
                next_out = dest,
                avail_out = (int)destLen,
            };

            // make second parameter negative to disable checksum verification
            int err = stream.inflateInit(-MAX_WBITS);
            if (err != zlibConst.Z_OK) return err;

            err = stream.inflate(zlibConst.Z_FINISH);
            if (err != zlibConst.Z_STREAM_END)
            {
                stream.inflateEnd();
                return err;
            }

            destLen = (ulong)stream.total_out;
            sourceLen = (ulong)stream.total_in;
            return stream.inflateEnd();
        }

        /// <summary>
        /// Uncompress a source byte array to a destination (old version)
        /// </summary>
        public static int UncompressOld(byte[] dest, ref ulong destLen, byte[] source, ref ulong sourceLen)
        {
            var stream = new ZStream
            {
                next_in = source,
                avail_in = (int)sourceLen,
                next_out = dest,
                avail_out = (int)destLen,
            };

            destLen = 0;
            sourceLen = 0;

            // make second parameter negative to disable checksum verification
            int err = stream.inflateInit(-MAX_WBITS);
            if (err != zlibConst.Z_OK)
                return err;

            while (stream.avail_in > 1)
            {
                err = stream.inflate(Z_BLOCK);
                if (err != zlibConst.Z_OK)
                {
                    stream.inflateEnd();
                    return err;
                }
            }

            destLen = (ulong)stream.total_out;
            sourceLen = (ulong)stream.total_in;
            return stream.inflateEnd();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Open a cabinet file for reading
        /// </summary>
        public Stream? OpenFileForReading(int index, string suffix)
        {
            if (string.IsNullOrWhiteSpace(filenamePattern))
                return null;

            string filename = $"{filenamePattern}{index}.{suffix}";
            if (File.Exists(filename))
                return File.OpenRead(filename);

            return null;
        }

        /// <summary>
        /// Get the start index of a pattern in a byte array
        /// </summary>
        private static int FindBytes(byte[] buffer, int offset, long bufferLeft, byte[] pattern)
        {
            while ((offset = Array.IndexOf(buffer, pattern[0], offset, (int)bufferLeft)) != -1)
            {
                if (pattern.Length > bufferLeft)
                    break;

#if NET40
                byte[] temp = new byte[pattern.Length];
                Array.Copy(buffer, offset, temp, 0, pattern.Length);
                if (temp.SequenceEqual(pattern))
                    return offset;
#else
                var temp = new ArraySegment<byte>(buffer, offset, pattern.Length);
                if (temp.SequenceEqual(pattern))
                    return offset;
#endif

                ++offset;
                --bufferLeft;
            }

            return -1;
        }

        /// <summary>
        /// Create the generic filename pattern to look for from the input filename
        /// </summary>
        private bool CreateFilenamePattern(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return false;

            filenamePattern = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filename))!, Path.GetFileNameWithoutExtension(filename));
            filenamePattern = new Regex(@"\d+$").Replace(filenamePattern, string.Empty);

            return true;
        }

        /// <summary>
        /// Read headers from the current file
        /// </summary>
        private bool ReadHeaders()
        {
            if (HeaderList != null)
            {
                Console.Error.WriteLine("Already have a header list");
                return true;
            }

            bool iterate = true;
            Header? previous = null;
            for (int i = 1; iterate; i++)
            {
                var file = OpenFileForReading(i, HEADER_SUFFIX);
                if (file != null)
                    iterate = false;
                else
                    file = OpenFileForReading(i, CABINET_SUFFIX);

                if (file == null)
                    break;

                var header = Header.Create(file, i);
                if (header == null)
                    break;

                if (previous != null)
                    previous.Next = header;
                else
                    previous = HeaderList = header;
            }

            return HeaderList != null;
        }

        #endregion
    }
}
