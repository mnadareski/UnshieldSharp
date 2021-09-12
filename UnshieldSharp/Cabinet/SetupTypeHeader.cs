using System.IO;

namespace UnshieldSharp.Cabinet
{
    public class SetupTypeHeader
    {
        public uint SetupTypesCount { get; private set; }
        public uint SetupTypeTableOffset { get; private set; }

        /// <summary>
        /// Create a new OffsetList from a stream and offset
        /// </summary>
        public static SetupTypeHeader Create(Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            SetupTypeHeader setupTypeHeader = new SetupTypeHeader();

            setupTypeHeader.SetupTypesCount = stream.ReadUInt32();
            setupTypeHeader.SetupTypeTableOffset = stream.ReadUInt32();

            return setupTypeHeader;
        }
    }
}
