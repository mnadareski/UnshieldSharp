using System;
using System.IO;

namespace UnshieldSharp
{
    public class CommonHeader
    {
        public uint Signature { get; private set; } // 00
        public uint Version { get; private set; }
        public uint VolumeInfo { get; private set; }
        public uint CabDescriptorOffset { get; private set; }
        public uint CabDescriptorSize { get; private set; } // 10

        /// <summary>
        /// Populate a CommonHeader from a stream
        /// </summary>
        public static CommonHeader Create(Stream stream)
        {
            byte[] buffer = new byte[Constants.COMMON_HEADER_SIZE];
            if (Constants.COMMON_HEADER_SIZE != stream.Read(buffer, 0, Constants.COMMON_HEADER_SIZE))
                return default;
            
            var commonHeader = new CommonHeader();
            int p = 0;

            commonHeader.Signature = BitConverter.ToUInt32(buffer, p); p += 4;
            if (commonHeader.Signature != Constants.CAB_SIGNATURE)
                return default;

            commonHeader.Version = BitConverter.ToUInt32(buffer, p); p += 4;
            commonHeader.VolumeInfo = BitConverter.ToUInt32(buffer, p); p += 4;
            commonHeader.CabDescriptorOffset = BitConverter.ToUInt32(buffer, p); p += 4;
            commonHeader.CabDescriptorSize = BitConverter.ToUInt32(buffer, p); p += 4;

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

                bytes = BitConverter.GetBytes(common.CabDescriptorOffset);
                foreach (byte b in bytes)
                    buffer[bufferPointer++] = b;

                bytes = BitConverter.GetBytes(common.CabDescriptorSize);
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
