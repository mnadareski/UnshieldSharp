using System;

namespace UnshieldSharp
{
    public class UnshieldComponent
    {
        public string Name { get; private set; }
        public uint FileGroupCount { get; private set; }
        public string[] FileGroupNames { get; private set; }
        public uint FileGroupNamesPointer { get; private set; } = 0;

        /// <summary>
        /// Create a new UnshieldComponent from a header and data offset
        /// </summary>
        public static UnshieldComponent Create(Header header, uint offset)
        {
            var component = new UnshieldComponent();
            int p = header.GetDataOffset(offset);
            
            component.Name = header.GetString((uint)p); p += 4;
            p += header.MajorVersion <= 5 ? 0x6c : 0x6b;
            component.FileGroupCount = BitConverter.ToUInt16(header.Data, p); p += 2;
            if (component.FileGroupCount > Constants.MAX_FILE_GROUP_COUNT)
                return default;

            component.FileGroupNamesPointer = BitConverter.ToUInt32(header.Data, p); p += 4;
            p = header.GetDataOffset(component.FileGroupNamesPointer);
            component.FileGroupNames = new string[component.FileGroupCount];
            for (int i = 0; i < component.FileGroupCount; i++)
            {
                component.FileGroupNames[i] = header.GetString((uint)p); p += 4;
            }

            return component;
        }
    }
}
