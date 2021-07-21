using System;
using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class CommonHeader
    {
        public uint Signature { get; private set; } // 00
        public uint Version { get; private set; }
        public uint VolumeInfo { get; private set; }
        public uint DescriptorOffset { get; private set; }
        public uint DescriptorSize { get; private set; } // 10

        /// <summary>
        /// Populate a CommonHeader from a stream
        /// </summary>
        public static CommonHeader Create(Stream stream)
        {
            if (stream.Length - stream.Position < Constants.COMMON_HEADER_SIZE)
                return null;

            var commonHeader = new CommonHeader();
            commonHeader.Signature = stream.ReadUInt32();
            if (commonHeader.Signature != Constants.CAB_SIGNATURE)
                return default;

            commonHeader.Version = stream.ReadUInt32();
            commonHeader.VolumeInfo = stream.ReadUInt32();
            commonHeader.DescriptorOffset = stream.ReadUInt32();
            commonHeader.DescriptorSize = stream.ReadUInt32();

            return commonHeader;
        }

        /// <summary>
        /// Write a CommonHeader to an input buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="bufferPointer"></param>
        /// <param name="common"></param>
        /// <returns></returns>
        public static bool WriteCommonHeader(ref byte[] buffer, int bufferPointer, CommonHeader common)
        {
            try
            {
                var bytes = BitConverter.GetBytes(Constants.CAB_SIGNATURE);
                foreach (byte b in bytes)
                    buffer[bufferPointer++] = b;

                bytes = BitConverter.GetBytes(common.Version);
                foreach (byte b in bytes)
                    buffer[bufferPointer++] = b;

                bytes = BitConverter.GetBytes(common.VolumeInfo);
                foreach (byte b in bytes)
                    buffer[bufferPointer++] = b;

                bytes = BitConverter.GetBytes(common.DescriptorOffset);
                foreach (byte b in bytes)
                    buffer[bufferPointer++] = b;

                bytes = BitConverter.GetBytes(common.DescriptorSize);
                foreach (byte b in bytes)
                    buffer[bufferPointer++] = b;
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
