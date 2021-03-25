using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnshieldSharp
{
    internal static class Extensions
    {
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

        public static byte ReadUInt8(this Stream stream)
        {
            byte[] buffer = new byte[1];
            stream.Read(buffer, 0, buffer.Length);
            return buffer[0];
        }

        public static ushort ReadUInt16(this Stream stream)
        {
            byte[] buffer = new byte[2];
            stream.Read(buffer, 0, buffer.Length);
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static uint ReadUInt32(this Stream stream)
        {
            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, buffer.Length);
            return BitConverter.ToUInt32(buffer, 0);
        }

        public static ulong ReadUInt64(this Stream stream)
        {
            byte[] buffer = new byte[8];
            stream.Read(buffer, 0, buffer.Length);
            return BitConverter.ToUInt64(buffer, 0);
        }
    }
}