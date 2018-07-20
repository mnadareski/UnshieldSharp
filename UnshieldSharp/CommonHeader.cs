using System;

namespace UnshieldSharp
{
    public class CommonHeader
    {
        public uint Signature; // 00
        public uint Version;
        public uint VolumeInfo;
        public uint CabDescriptorOffset;
        public uint CabDescriptorSize; // 10

        /// <summary>
        /// Populate a CommonHeader from an input buffer
        /// </summary>
        public static bool ReadCommonHeader(ref byte[] buffer, int bufferPointer, CommonHeader common)
        {
            common.Signature = BitConverter.ToUInt32(buffer, bufferPointer); bufferPointer += 4;

            if (common.Signature != Constants.CAB_SIGNATURE)
                return false;

            common.Version = BitConverter.ToUInt32(buffer, bufferPointer); bufferPointer += 4;
            common.VolumeInfo = BitConverter.ToUInt32(buffer, bufferPointer); bufferPointer += 4;
            common.CabDescriptorOffset = BitConverter.ToUInt32(buffer, bufferPointer); bufferPointer += 4;
            common.CabDescriptorSize = BitConverter.ToUInt32(buffer, bufferPointer); bufferPointer += 4;

            return true;
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
