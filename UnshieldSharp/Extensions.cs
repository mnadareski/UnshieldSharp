using System.IO;
using System.Text;
using SabreTools.IO.Extensions;

namespace UnshieldSharp
{
    internal static class Extensions
    {
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
        /// Get the zlib result name from an integer
        /// </summary>
        /// <param name="result">Integer to translate to the result name</param>
        /// <returns>Name of the result, the integer as a string otherwise</returns>
        public static string ToZlibConstName(this int result)
        {
            switch (result)
            {
                case 0:
                    return "Z_OK";
                case 1:
                    return "Z_STREAM_END";
                case 2:
                    return "Z_NEED_DICT";
                case -1:
                    return "Z_ERRNO";
                case -2:
                    return "Z_STREAM_ERROR";
                case -3:
                    return "Z_DATA_ERROR";
                case -4:
                    return "Z_MEM_ERROR";
                case -5:
                    return "Z_BUF_ERROR";
                case -6:
                    return "Z_VERSION_ERROR";
                default:
                    return result.ToString();
            }
        }
    }
}