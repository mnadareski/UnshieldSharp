namespace UnshieldSharp
{
    internal static class Extensions
    {
        /// <summary>
        /// Get the zlib result name from an integer
        /// </summary>
        /// <param name="result">Integer to translate to the result name</param>
        /// <returns>Name of the result, the integer as a string otherwise</returns>
        public static string ToZlibConstName(this int result)
        {
            return result switch
            {
                0 => "Z_OK",
                1 => "Z_STREAM_END",
                2 => "Z_NEED_DICT",

                -1 => "Z_ERRNO",
                -2 => "Z_STREAM_ERROR",
                -3 => "Z_DATA_ERROR",
                -4 => "Z_MEM_ERROR",
                -5 => "Z_BUF_ERROR",
                -6 => "Z_VERSION_ERROR",

                _ => result.ToString(),
            };
        }
    }
}