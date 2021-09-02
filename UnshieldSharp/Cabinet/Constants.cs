namespace UnshieldSharp.Cabinet
{
    internal static class Constants
    {
        #region cabfile.h

        public const int OFFSET_COUNT = 0x47;
        public const int CAB_SIGNATURE = 0x28635349;

        public const int MSCF_SIGNATURE = 0x4643534d;

        public const int COMMON_HEADER_SIZE = 20;
        public const int VOLUME_HEADER_SIZE_V5 = 40;
        public const int VOLUME_HEADER_SIZE_V6 = 64;

        public const int MAX_FILE_GROUP_COUNT = 81; // Originally 71 - Hangs on 82+
        public const int MAX_COMPONENT_COUNT = 81; // Originally 71 - Hangs on 82+

        #endregion

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