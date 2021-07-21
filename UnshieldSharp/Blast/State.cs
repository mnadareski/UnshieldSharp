using System;
using System.Collections.Generic;
using System.Linq;

namespace UnshieldSharp.Blast
{
    /// <summary>
    /// Input and output state
    /// </summary>
    public class State
    {
        #region Input State

        /// <summary>
        /// Opaque information passed to InputFunction()
        /// </summary>
        public byte[] InHow;

        /// <summary>
        /// Pointer to opaque information
        /// </summary>
        public int InHowPtr;
        
        /// <summary>
        /// Next input location
        /// </summary>
        public List<byte> Input;
        
        /// <summary>
        /// Pointer to the next input location
        /// </summary>
        public int InputPtr;
        
        /// <summary>
        /// Available input at in
        /// </summary>
        public uint Left;
        
        /// <summary>
        /// Bit buffer
        /// </summary>
        public int BitBuf;
        
        /// <summary>
        /// Number of bits in bit buffer
        /// </summary>
        public int BitCnt;

        #endregion

        #region Output State

        /// <summary>
        /// Opaque information passed to OutputFunction()
        /// </summary>
        public List<byte> OutHow;

        /// <summary>
        /// Pointer to opaque information
        /// </summary>
        public int OutHowPtr;
        
        /// <summary>
        /// Index of next write location in out[]
        /// </summary>
        public uint Next;
        
        /// <summary>
        /// True to check distances (for first 4K)
        /// </summary>
        public bool First;
        
        /// <summary>
        /// Output buffer and sliding window
        /// </summary>
        public byte[] Output = new byte[Constants.MAXWIN];

        /// <summary>
        /// Pointer to the next output location
        /// </summary>
        public int OutputPtr;

        #endregion

        /// <summary>
        /// Return need bits from the input stream.  This always leaves less than
        /// eight bits in the buffer.  bits() works properly for need == 0.
        /// </summary>
        /// <param name="need">Number of bits to read</param>
        /// <remarks>
        /// Bits are stored in bytes from the least significant bit to the most
        /// significant bit.  Therefore bits are dropped from the bottom of the bit
        /// buffer, using shift right, and new bytes are appended to the top of the
        /// bit buffer, using shift left.
        /// </remarks>
        public int Bits(int need)
        {
            // Load at least need bits into val
            int val = BitBuf;
            while (BitCnt < need)
            {
                if (Left == 0)
                {
                    Left = ProcessInput();
                    if (Left == 0)
                        throw new IndexOutOfRangeException();
                }

                // Load eight bits
                val |= Input[InputPtr++] << BitCnt;
                Left--;
                BitCnt += 8;
            }

            // Drop need bits and update buffer, always zero to seven bits left
            BitBuf = val >> need;
            BitCnt -= need;

            // Return need bits, zeroing the bits above that
            return val & ((1 << need) - 1);
        }

        /// <summary>
        /// Process input for the current state
        /// </summary>
        /// <returns>Amount of data in Input</returns>
        public uint ProcessInput()
        {
            Input = new List<byte>(InHow);
            return (uint)Input.Count;
        }

        /// <summary>
        /// Process output for the current state
        /// </summary>
        /// <returns>True if the output could be added, false otherwise</returns>
        public bool ProcessOutput()
        {
            try
            {
                OutHow.AddRange(Output.Take((int)Next));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}