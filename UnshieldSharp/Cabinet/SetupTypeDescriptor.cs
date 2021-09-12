using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class SetupTypeDescriptor
    {
        public uint NameOffset { get; private set; }
        public uint DescriptorOffset { get; private set; }
        public uint DisplayNameOffset { get; private set; }
        public uint SetupTableCount { get; private set; }
        public uint SetupTableOffset { get; private set; }

        /// <summary>
        /// Create a new OffsetList from a stream and offset
        /// </summary>
        public static SetupTypeDescriptor Create(Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            SetupTypeDescriptor setupTypeDescriptor = new SetupTypeDescriptor();

            setupTypeDescriptor.NameOffset = stream.ReadUInt32();
            setupTypeDescriptor.DescriptorOffset = stream.ReadUInt32();
            setupTypeDescriptor.DisplayNameOffset = stream.ReadUInt32();
            setupTypeDescriptor.SetupTableCount = stream.ReadUInt32();
            setupTypeDescriptor.SetupTableOffset = stream.ReadUInt32();

            return setupTypeDescriptor;
        }
    }
}
