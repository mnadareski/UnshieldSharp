using System;
using System.IO;
using System.Text.RegularExpressions;
using SabreTools.Compression.zlib;
using SabreTools.Hashing;
using SabreTools.Models.InstallShieldCabinet;
using static SabreTools.Models.InstallShieldCabinet.Constants;
using Header = SabreTools.Serialization.Wrappers.InstallShieldCabinet;

namespace UnshieldSharp
{
    // TODO: Figure out if individual parts of a split cab can be extracted separately
    public class InstallShieldCabinet
    {
        /// <summary>
        /// Linked CAB headers
        /// </summary>
        public Header? HeaderList { get; private set; }

        /// <summary>
        /// Base filename path for related CAB files
        /// </summary>
        private string? filenamePattern;

        /// <summary>
        /// Default buffer size
        /// </summary>
        private const int BUFFER_SIZE = 64 * 1024;

        /// <summary>
        /// Maximum size of the window in bits
        /// </summary>
        private const int MAX_WBITS = 15;

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

        #region File

        /// <summary>
        /// Save the file at the given index to the filename specified
        /// </summary>
        public bool FileSave(int index, string filename)
        {
            if (HeaderList == null)
            {
                Console.Error.WriteLine("Header list is not built");
                return false;
            }

            // Get the file descriptor
            var fileDescriptor = GetFileDescriptor(index);
            if (fileDescriptor == null)
                return false;

            // If the file is split
            if (fileDescriptor.LinkFlags == LinkFlags.LINK_PREV)
                return FileSave((int)fileDescriptor.LinkPrevious, filename);

            // Get the reader at the index
            var reader = Reader.Create(this, index, fileDescriptor);
            if (reader == null)
                return false;

            // Create the output file and hasher
            FileStream output = File.OpenWrite(filename);
            var md5 = new HashWrapper(HashType.MD5);

            ulong bytesLeft = GetBytesToRead(fileDescriptor);
            byte[] inputBuffer;
            byte[] outputBuffer = new byte[BUFFER_SIZE];
            ulong totalWritten = 0;

            // Read while there are bytes remaining
            while (bytesLeft > 0)
            {
                ulong bytesToWrite = BUFFER_SIZE;
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
                        Console.Error.WriteLine($"Failed to read {lengthArr.Length} bytes of file {index} ({HeaderList.GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Validate the number of bytes to read
                    ushort bytesToRead = BitConverter.ToUInt16(lengthArr, 0);
                    if (bytesToRead == 0)
                    {
                        Console.Error.WriteLine("bytesToRead can't be zero");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Attempt to read the specified number of bytes
                    inputBuffer = new byte[BUFFER_SIZE + 1];
                    if (!reader.Read(inputBuffer, 0, bytesToRead))
                    {
                        Console.Error.WriteLine($"Failed to read {lengthArr.Length} bytes of file {index} ({HeaderList.GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    // Add a null byte to make inflate happy
                    inputBuffer[bytesToRead] = 0;
                    ulong readBytes = (ulong)(bytesToRead + 1);

                    // Uncompress into a buffer
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
                    bytesLeft -= 2;
                    bytesLeft -= bytesToRead;
                }

                // Handle uncompressed files
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

                    // Set remaining bytes
                    bytesLeft -= (uint)bytesToWrite;
                }

                // Hash and write the next block
                md5.Process(outputBuffer, 0, (int)bytesToWrite);
                output?.Write(outputBuffer, 0, (int)bytesToWrite);
                totalWritten += bytesToWrite;
            }

            // Validate the number of bytes written
            if (fileDescriptor.ExpandedSize != totalWritten)
            {
                Console.Error.WriteLine($"Expanded size expected to be {fileDescriptor.ExpandedSize}, but was {totalWritten}");
                reader.Dispose();
                output?.Close();
                return false;
            }

            // Finalize output values
            md5.Terminate();
            reader?.Dispose();
            output?.Close();

            // Validate the data written, if required
            if (HeaderList!.MajorVersion >= 6)
            {
                string? md5result = md5.CurrentHashString;
                if (md5result == null || md5result != BitConverter.ToString(fileDescriptor.MD5!))
                {
                    Console.Error.WriteLine($"MD5 checksum failure for file {index} ({HeaderList.GetFileName(index)})");
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
            if (HeaderList == null)
            {
                Console.Error.WriteLine("Header list is not built");
                return false;
            }

            // Get the file descriptor
            var fileDescriptor = GetFileDescriptor(index);
            if (fileDescriptor == null)
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

            ulong bytesLeft = GetBytesToRead(fileDescriptor);
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
        /// Common code for getting the bytes to read
        /// </summary>
        private static ulong GetBytesToRead(FileDescriptor fd)
        {
#if NET20 || NET35
            if ((fd.Flags & FileFlags.FILE_COMPRESSED) != 0)
#else
            if (fd.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
#endif
                return fd.CompressedSize;
            else
                return fd.ExpandedSize;
        }

        /// <summary>
        /// Common code for getting the file descriptor
        /// </summary>
        private FileDescriptor? GetFileDescriptor(int index)
        {
            if (HeaderList == null)
            {
                Console.Error.WriteLine("Header list is not built");
                return null;
            }

            var fileDescriptor = HeaderList.GetFileDescriptor(index);
            if (fileDescriptor == null)
            {
                Console.Error.WriteLine($"Failed to get file descriptor for file {index}");
                return null;
            }

#if NET20 || NET35
            if ((fileDescriptor.Flags & FileFlags.FILE_INVALID) != 0 || fileDescriptor.DataOffset == 0)
#else
            if (fileDescriptor.Flags.HasFlag(FileFlags.FILE_INVALID) || fileDescriptor.DataOffset == 0)
#endif
            {
                Console.Error.WriteLine($"File at {index} is marked as invalid");
                return null;
            }

            return fileDescriptor;
        }

        #endregion

        #region Uncompression

        /// <summary>
        /// Uncompress a source byte array to a destination
        /// </summary>
        internal unsafe static int Uncompress(byte[] dest, ref ulong destLen, byte[] source, ref ulong sourceLen)
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
                if (err != zlibConst.Z_STREAM_END)
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
        internal unsafe static int UncompressOld(byte[] dest, ref ulong destLen, byte[] source, ref ulong sourceLen)
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

        #region Helpers

        /// <summary>
        /// Open a cabinet file for reading
        /// </summary>
        public Stream? OpenFileForReading(int index, string suffix)
        {
            if (string.IsNullOrEmpty(filenamePattern))
                return null;

            // Attempt lower-case extension
            string filename = $"{filenamePattern}{index}.{suffix}";
            if (File.Exists(filename))
                return File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Attempt upper-case extension
            filename = $"{filenamePattern}{index}.{suffix.ToUpperInvariant()}";
            if (File.Exists(filename))
                return File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            return null;
        }

        /// <summary>
        /// Create the generic filename pattern to look for from the input filename
        /// </summary>
        private bool CreateFilenamePattern(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return false;

            string? directory = Path.GetDirectoryName(Path.GetFullPath(filename));
            if (directory != null)
                filenamePattern = Path.Combine(directory, Path.GetFileNameWithoutExtension(filename));
            else
                filenamePattern = Path.GetFileNameWithoutExtension(filename);

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

                var header = Header.Create(file);
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
