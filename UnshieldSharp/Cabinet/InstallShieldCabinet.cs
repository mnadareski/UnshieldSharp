using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SabreTools.Compression.zlib;
using SabreTools.Models.InstallShieldCabinet;
using static SabreTools.Models.InstallShieldCabinet.Constants;
using Header = SabreTools.Serialization.Wrappers.InstallShieldCabinet;

namespace UnshieldSharp.Cabinet
{
    // TODO: Figure out if individual parts of a split cab can be extracted separately
    public class InstallShieldCabinet
    {
        // Linked CAB headers
        public Header? HeaderList { get; private set; }

        // Base filename path for related CAB files
        private string? filenamePattern;

        // Default buffer size
        private const int BUFFER_SIZE = 64 * 1024;

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

#if NET20 || NET35
                if ((fileDescriptor.Flags & FileFlags.FILE_COMPRESSED) != 0)
#else
                if (fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
#endif
                {
                    ulong readBytes;
                    byte[] bytesToRead = new byte[sizeof(ushort)];

                    // Attempt to read the length value
                    if (!reader.Read(bytesToRead, 0, bytesToRead.Length))
                    {
                        Console.Error.WriteLine($"Failed to read {bytesToRead.Length} bytes of file {index} ({HeaderList.GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
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
                        Console.Error.WriteLine($"Failed to read {bytesToRead.Length} bytes of file {index} ({HeaderList.GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
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
                    Console.Error.WriteLine($"MD5 checksum failure for file {index} ({HeaderList.GetFileName(index)})");
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
            if (HeaderList == null)
            {
                Console.Error.WriteLine("Header list is not built");
                return false;
            }

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

            ulong bytesLeft = GetBytesToRead(fileDescriptor);
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

#if NET20 || NET35
                if ((fileDescriptor.Flags & FileFlags.FILE_COMPRESSED) != 0)
#else
                if (fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED))
#endif
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
                        Console.Error.WriteLine($"Failed to read {inputSize} bytes of file {index} ({HeaderList.GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
                        reader.Dispose();
                        output?.Close();
                        return false;
                    }

                    bytesLeft -= (uint)inputSize;
                    for (int p = 0; inputSize > 0;)
                    {
                        int match = FindBytes(inputBuffer, p, inputSize, END_OF_CHUNK);
                        if (match == -1)
                        {
                            Console.Error.WriteLine($"Could not find end of chunk for file {index} ({HeaderList.GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
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
                            match = FindBytes(inputBuffer, (int)(p + chunkSize), inputSize - chunkSize, END_OF_CHUNK);
                            if (match == -1)
                            {
                                Console.Error.WriteLine($"Could not find end of chunk for file {index} ({HeaderList.GetFileName(index)}) from input cabinet file {fileDescriptor.Volume}");
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
            if (HeaderList == null)
            {
                Console.Error.WriteLine("Header list is not built");
                return false;
            }

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

            ulong bytesLeft = GetBytesToRead(fileDescriptor);
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
        private FileDescriptor? GetFileDescriptor(string filename, int index)
        {
            if (HeaderList == null)
            {
                Console.Error.WriteLine("Header list is not built");
                return null;
            }

            if (string.IsNullOrEmpty(filename))
            {
                Console.Error.WriteLine("Provided filename is invalid");
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

        /// <summary>
        /// Common code for getting the reader
        /// </summary>
        private Reader? GetReader(int index, FileDescriptor fd)
        {
            var reader = Reader.Create(this, index, fd);
            if (reader?.VolumeFile == null)
            {
                Console.Error.WriteLine($"Failed to create data reader for file {index}");
                return null;
            }

            if (reader.VolumeFile.Length == (long)fd.DataOffset)
            {
                Console.Error.WriteLine($"File {index} is not inside the cabinet.");
                reader.Dispose();
                return null;
            }

            return reader;
        }

        #endregion

        #region Uncompression

        /// <summary>
        /// Uncompress a source byte array to a destination
        /// </summary>
        public unsafe static int Uncompress(byte[] dest, ref ulong destLen, byte[] source, ref ulong sourceLen)
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
                int err = ZLib.inflateInit_(stream, ZLib.zlibVersion(), source.Length);
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
        public unsafe static int UncompressOld(byte[] dest, ref ulong destLen, byte[] source, ref ulong sourceLen)
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
                int err = ZLib.inflateInit_(stream, ZLib.zlibVersion(), source.Length);
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

            string filename = $"{filenamePattern}{index}.{suffix}";
            if (File.Exists(filename))
                return File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

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

#if NET20 || NET35 || NET40
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
