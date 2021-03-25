using System;

namespace UnshieldSharp
{
    [Flags]
    public enum FileDescriptorFlag : ushort
    {
        FILE_SPLIT = 1,
        FILE_OBFUSCATED = 2,
        FILE_COMPRESSED = 4,
        FILE_INVALID = 8,
    }

    public enum FileDescriptorLinkFlag : byte
    {
        LINK_NONE = 0,
        LINK_PREV = 1,
        LINK_NEXT = 2,
        LINK_BOTH = 3,
    }
}