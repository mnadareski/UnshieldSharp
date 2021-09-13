using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class Component
    {
        public uint IdentifierOffset { get; private set; }
        public string Identifier { get; private set; }
        public uint DescriptorOffset { get; private set; }
        public uint DisplayNameOffset { get; private set; }
        public ushort Reserved0 { get; private set; }
        public uint ReservedOffset0 { get; private set; }
        public uint ReservedOffset1 { get; private set; }
        public ushort ComponentIndex { get; private set; }
        public uint NameOffset { get; private set; }
        public uint ReservedOffset2 { get; private set; }
        public uint ReservedOffset3 { get; private set; }
        public uint ReservedOffset4 { get; private set; }
        public uint[] Reserved1 { get; private set; } = new uint[8];
        public uint CLSIDOffset { get; private set; }
        public uint[] Reserved2 { get; private set; } = new uint[7];
        public ushort Reserved3 { get; private set; } // ushort for versions below 6, byte above that
        public ushort DependsCount { get; private set; }
        public uint DependsOffset { get; private set; }
        public uint FileGroupCount { get; private set; }
        public uint FileGroupNamesOffset { get; private set; }
        public string[] FileGroupNames { get; private set; }
        public ushort X3Count { get; private set; }
        public uint X3Offset { get; private set; }
        public ushort SubComponentsCount { get; private set; }
        public uint SubComponentsOffset { get; private set; }
        public uint NextComponentOffset { get; private set; }
        public uint ReservedOffset5 { get; private set; }
        public uint ReservedOffset6 { get; private set; }
        public uint ReservedOffset7 { get; private set; }
        public uint ReservedOffset8 { get; private set; }

        /// <summary>
        /// Create a new Component from a header and data offset
        /// </summary>
        public static Component Create(Header header, uint offset)
        {
            int dataOffset = header.GetDataOffset(offset);
            if (dataOffset < 0 || dataOffset >= header.Data.Length)
                return null;

            header.Data.Seek(dataOffset, SeekOrigin.Begin);

            var component = new Component();

            component.IdentifierOffset = header.Data.ReadUInt32();
            component.Identifier = header.GetString(component.IdentifierOffset);
            component.DescriptorOffset = header.Data.ReadUInt32();
            component.DisplayNameOffset = header.Data.ReadUInt32();
            component.Reserved0 = header.Data.ReadUInt16();
            component.ReservedOffset0 = header.Data.ReadUInt32();
            component.ReservedOffset1 = header.Data.ReadUInt32();
            component.ComponentIndex = header.Data.ReadUInt16();
            component.NameOffset = header.Data.ReadUInt32();
            component.ReservedOffset2 = header.Data.ReadUInt32();
            component.ReservedOffset3 = header.Data.ReadUInt32();
            component.ReservedOffset4 = header.Data.ReadUInt32();

            component.Reserved1 = new uint[8];
            for (int i = 0; i < 8; i++)
            {
                component.Reserved1[i] = header.Data.ReadUInt32();
            }

            component.CLSIDOffset = header.Data.ReadUInt32();

            component.Reserved2 = new uint[7];
            for (int i = 0; i < 7; i++)
            {
                component.Reserved2[i] = header.Data.ReadUInt32();
            }

            component.Reserved3 = header.MajorVersion <= 5 ? header.Data.ReadUInt16() : header.Data.ReadUInt8();
            component.DependsCount = header.Data.ReadUInt16();
            component.DependsOffset = header.Data.ReadUInt32(); // TODO: Read this into a table

            component.FileGroupCount = header.Data.ReadUInt16();
            component.FileGroupNamesOffset = header.Data.ReadUInt32();
            dataOffset = header.GetDataOffset(component.FileGroupNamesOffset);
            component.FileGroupNames = new string[component.FileGroupCount];
            for (int i = 0; i < component.FileGroupCount; i++)
            {
                component.FileGroupNames[i] = header.GetString((uint)dataOffset); dataOffset += 4;
            }

            component.X3Count = header.Data.ReadUInt16();
            component.X3Offset = header.Data.ReadUInt32();
            component.SubComponentsCount = header.Data.ReadUInt16();
            component.SubComponentsOffset = header.Data.ReadUInt32();
            component.NextComponentOffset = header.Data.ReadUInt32();
            component.ReservedOffset5 = header.Data.ReadUInt32();
            component.ReservedOffset6 = header.Data.ReadUInt32();
            component.ReservedOffset7 = header.Data.ReadUInt32();
            component.ReservedOffset8 = header.Data.ReadUInt32();

            return component;
        }
    }
}
