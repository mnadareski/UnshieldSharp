using System;

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
        public byte[] Input;
        
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
        public byte[] OutHow;

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
            // TODO: Verify that this matches the original implementation properly
            // int _blast_out(void *how, unsigned char *buf, unsigned len) {
            //     std::vector<unsigned char> *outbuf = reinterpret_cast<std::vector<unsigned char>*>(how);
            //     outbuf->insert(outbuf->end(), &buf[0], &buf[len]);
            //     return false; // would indicate write error
            // }

            int length = InHow.Length - InHowPtr;
            Array.Copy(InHow, InHowPtr, Input, InputPtr, length);
            return (uint)length;
        }

        /// <summary>
        /// Process output for the current state
        /// </summary>
        public int ProcessOutput()
        {
            // TODO: Verify that this matches the original implementation properly
            // unsigned _blast_in(void *how, unsigned char **buf) {
            //     std::vector<unsigned char> *inbuf = reinterpret_cast<std::vector<unsigned char>*>(how);
            //     *buf = inbuf->data();
            //     return unsigned(inbuf->size());
            // }

            Array.Copy(Output, 0, OutHow, OutHowPtr, Next);
            return 0; // would indicate write error
        }
    }
}