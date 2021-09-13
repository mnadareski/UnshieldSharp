using System;
using System.IO;
using static UnshieldSharp.Cabinet.Constants;

namespace UnshieldSharp.Cabinet
{
    public class CommonHeader
    {
        public uint Signature { get; private set; }
        public uint Version { get; private set; }
        public int MajorVersion
        {
            get
            {
                int version = default;
                if ((this.Version >> 24) == 1)
                {
                    version = (int)((Version >> 12) & 0xf);
                }
                else if ((Version >> 24) == 2 || (Version >> 24) == 4)
                {
                    version = (int)(Version & 0xffff);
                    if (version != 0)
                        version /= 100;
                }

                return version;
            }
        }
        public byte NextVolume { get; private set; }
        public byte Reserved0 { get; private set; }
        public ushort Reserved1 { get; private set; }
        public uint DescriptorOffset { get; private set; }
        public uint DescriptorSize { get; private set; }

        /// <summary>
        /// Populate a CommonHeader from a stream
        /// </summary>
        public static CommonHeader Create(Stream stream)
        {
            if (stream.Length - stream.Position < COMMON_HEADER_SIZE)
                return null;

            var commonHeader = new CommonHeader();
            commonHeader.Signature = stream.ReadUInt32();
            if (commonHeader.Signature != CAB_SIGNATURE)
            {
                if (commonHeader.Signature == MSCF_SIGNATURE)
                    Console.WriteLine("Microsoft CAB found! This cannot be extracted by this library.");
                else
                    Console.WriteLine("Non-InstallShield file found! This cannot be extracted by this library.");

                return default;
            }

            commonHeader.Version = stream.ReadUInt32();
            commonHeader.NextVolume = stream.ReadUInt8();
            commonHeader.Reserved0 = stream.ReadUInt8();
            commonHeader.Reserved1 = stream.ReadUInt16();
            commonHeader.DescriptorOffset = stream.ReadUInt32();
            commonHeader.DescriptorSize = stream.ReadUInt32();

            return commonHeader;
        }
    }
}
