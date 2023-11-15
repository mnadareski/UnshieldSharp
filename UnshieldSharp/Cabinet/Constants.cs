namespace UnshieldSharp.Cabinet
{
    internal static class Constants
    {
        #region file.c

        public const int BUFFER_SIZE = 64 * 1024;

        #endregion

        #region internal.h

        public const string HEADER_SUFFIX = "hdr";
        public const string CABINET_SUFFIX = "cab";

        #endregion

        #region libunshield.h

        public const int UNSHIELD_LOG_LEVEL_LOWEST = 0;

        public const int UNSHIELD_LOG_LEVEL_ERROR = 1;
        public const int UNSHIELD_LOG_LEVEL_WARNING = 2;
        public const int UNSHIELD_LOG_LEVEL_TRACE = 3;

        public const int UNSHIELD_LOG_LEVEL_HIGHEST = 4;

        #endregion

        #region zconf.h

        public const int MAX_WBITS = 15;
        public const int Z_BLOCK = 5;

        #endregion
    }
}