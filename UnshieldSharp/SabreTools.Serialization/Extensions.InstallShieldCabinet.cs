using SabreTools.Models.InstallShieldCabinet;

namespace SabreTools.Serialization
{
    public static partial class Extensions
    {
        #region File Descriptors

        /// <summary>
        /// Indicate if a file descriptor represents a compressed file
        /// </summary>
        /// <param name="fileDescriptor">File descriptor to check</param>
        /// <returns>True if the file is flagged as compressed, false otherwise</returns>
        public static bool IsCompressed(this FileDescriptor? fileDescriptor)
        {
            // Ignore invalid descriptors
            if (fileDescriptor == null)
                return true;

#if NET20 || NET35
            return (fileDescriptor.Flags & FileFlags.FILE_COMPRESSED) != 0;
#else
            return fileDescriptor.Flags.HasFlag(FileFlags.FILE_COMPRESSED);
#endif
        }

        /// <summary>
        /// Indicate if a file descriptor represents an invalid file
        /// </summary>
        /// <param name="fileDescriptor">File descriptor to check</param>
        /// <returns>True if the file is flagged as invalid, false otherwise</returns>
        public static bool IsInvalid(this FileDescriptor? fileDescriptor)
        {
            // Ignore invalid descriptors
            if (fileDescriptor == null)
                return true;

#if NET20 || NET35
            return (fileDescriptor.Flags & FileFlags.FILE_INVALID) != 0;
#else
            return fileDescriptor.Flags.HasFlag(FileFlags.FILE_INVALID);
#endif
        }

        /// <summary>
        /// Indicate if a file descriptor represents an obfuscated file
        /// </summary>
        /// <param name="fileDescriptor">File descriptor to check</param>
        /// <returns>True if the file is flagged as obfuscated, false otherwise</returns>
        public static bool IsObfuscated(this FileDescriptor? fileDescriptor)
        {
            // Ignore invalid descriptors
            if (fileDescriptor == null)
                return false;

#if NET20 || NET35
            return (fileDescriptor.Flags & FileFlags.FILE_OBFUSCATED) != 0;
#else
            return fileDescriptor.Flags.HasFlag(FileFlags.FILE_OBFUSCATED);
#endif
        }

        /// <summary>
        /// Indicate if a file descriptor represents a split file
        /// </summary>
        /// <param name="fileDescriptor">File descriptor to check</param>
        /// <returns>True if the file is flagged as split, false otherwise</returns>
        public static bool IsSplit(this FileDescriptor? fileDescriptor)
        {
            // Ignore invalid descriptors
            if (fileDescriptor == null)
                return false;

#if NET20 || NET35
            return (fileDescriptor.Flags & FileFlags.FILE_SPLIT) != 0;
#else
            return fileDescriptor.Flags.HasFlag(FileFlags.FILE_SPLIT);
#endif
        }

        #endregion

        #region Version

        /// <summary>
        /// Get the major version of an InstallShield Cabinet
        /// </summary>
        /// <param name="cabinet">Cabinet to derive the version from</param>
        /// <returns>Major version of the cabinet, -1 on error</returns>
        public static int GetMajorVersion(this Cabinet? cabinet)
        {
            // Ignore invalid cabinets
            if (cabinet == null)
                return -1;

            return cabinet.CommonHeader.GetMajorVersion();
        }

        /// <summary>
        /// Get the major version of an InstallShield Cabinet
        /// </summary>
        /// <param name="commonHeader">CommonHeader to derive the version from</param>
        /// <returns>Major version of the cabinet, -1 on error</returns>
        public static int GetMajorVersion(this CommonHeader? commonHeader)
        {
            // Ignore invalid headers
            if (commonHeader == null)
                return -1;

            uint majorVersion = commonHeader.Version;
            if (majorVersion >> 24 == 1)
            {
                majorVersion = (majorVersion >> 12) & 0x0F;
            }
            else if (majorVersion >> 24 == 2 || majorVersion >> 24 == 4)
            {
                majorVersion &= 0xFFFF;
                if (majorVersion != 0)
                    majorVersion /= 100;
            }

            return (int)majorVersion;
        }

        #endregion
    }
}