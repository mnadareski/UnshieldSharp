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
        /// Process input for the current state
        /// </summary>
        public uint ProcessInput()
        {
            Input = new List<byte>(InHow);
            return (uint)Input.Count;
        }

        /// <summary>
        /// Process output for the current state
        /// </summary>
        public int ProcessOutput()
        {
            OutHow.AddRange(Output.Take((int)Next));
            return 0; // would indicate write error
        }
    }
}