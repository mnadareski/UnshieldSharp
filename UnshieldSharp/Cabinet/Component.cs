using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class Component
    {
        public string Name { get; private set; }
        public uint FileGroupCount { get; private set; }
        public string[] FileGroupNames { get; private set; }
        public uint FileGroupNamesPointer { get; private set; } = 0;

        /// <summary>
        /// Create a new Component from a header and data offset
        /// </summary>
        public static Component Create(Header header, uint offset)
        {
            var component = new Component();
            int p = header.GetDataOffset(offset);
            
            component.Name = header.GetString((uint)p); p += 4;
            p += header.MajorVersion <= 5 ? 0x6c : 0x6b;
            header.Data.Seek(p, SeekOrigin.Begin);
            component.FileGroupCount = header.Data.ReadUInt16();
            if (component.FileGroupCount > Constants.MAX_FILE_GROUP_COUNT)
                return default;

            component.FileGroupNamesPointer = header.Data.ReadUInt32();
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
