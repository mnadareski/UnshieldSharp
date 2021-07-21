using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnshieldSharp
{
    internal static class Extensions
    {
        /// <summary>
        /// Read a null-terminated string from the stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public static string ReadNullTerminatedString(this Stream stream)
        {
            List<byte> buffer = new List<byte>();
            byte b;
            while ((b = (byte)stream.ReadByte()) != 0x00)
            {
                buffer.Add(b);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        /// <summary>
        /// Read a string whose length is determined by a 1-byte header from the stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public static string ReadUInt8HeaderedString(this Stream stream)
        {
            byte len = stream.ReadUInt8();
            byte[] buf = stream.ReadBytes(len);
            return Encoding.ASCII.GetString(buf, 0, len);
        }

        /// <summary>
        /// Read a string whose length is determined by a 2-byte header from the stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public static string ReadUInt16HeaderedString(this Stream stream)
        {
            ushort len = stream.ReadUInt16();
            byte[] buf = stream.ReadBytes(len);
            return Encoding.ASCII.GetString(buf, 0, len);
        }

        /// <summary>
        /// Read a byte from the stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public static byte ReadUInt8(this Stream stream)
        {
            byte[] buffer = new byte[1];
            stream.Read(buffer, 0, buffer.Length);
            return buffer[0];
        }

        /// <summary>
        /// Read a ushort from the stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public static ushort ReadUInt16(this Stream stream)
        {
            byte[] buffer = new byte[2];
            stream.Read(buffer, 0, buffer.Length);
            return BitConverter.ToUInt16(buffer, 0);
        }

        /// <summary>
        /// Read a uint from the stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public static uint ReadUInt32(this Stream stream)
        {
            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, buffer.Length);
            return BitConverter.ToUInt32(buffer, 0);
        }

        /// <summary>
        /// Read a ulong from the stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public static ulong ReadUInt64(this Stream stream)
        {
            byte[] buffer = new byte[8];
            stream.Read(buffer, 0, buffer.Length);
            return BitConverter.ToUInt64(buffer, 0);
        }

        /// <summary>
        /// Read a byte array from the stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public static byte[] ReadBytes(this Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            stream.Read(buffer, 0, count);
            return buffer;
        }
    }
}