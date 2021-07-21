using System;

namespace UnshieldSharp.Blast
{
    /// <summary>
    /// Huffman code decoding tables.  count[1..MAXBITS] is the number of symbols of
    /// each length, which for a canonical code are stepped through in order.
    /// symbol[] are the symbol values in canonical order, where the number of
    /// entries is the sum of the counts in count[].  The decoding process can be
    /// seen in the function decode() below.
    /// </summary>
    public class Huffman
    {
        /// <summary>
        /// Number of symbols of each length
        /// </summary>
        public short[] Count { get; set; }

        /// <summary>
        /// Pointer to number of symbols of each length
        /// </summary>
        public int CountPtr { get; set; }

        /// <summary>
        /// Canonically ordered symbols
        /// </summary>
        public short[] Symbol { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="countLength">Length of the Count array</param>
        /// <param name="symbolLength">Length of the Symbol array</param>
        public Huffman(int countLength, int symbolLength)
        {
            Count = new short[countLength];
            Symbol = new short[symbolLength];
        }

        /// <summary>
        /// Given a list of repeated code lengths rep[0..n-1], where each byte is a
        /// count (high four bits + 1) and a code length (low four bits), generate the
        /// list of code lengths.  This compaction reduces the size of the object code.
        /// Then given the list of code lengths length[0..n-1] representing a canonical
        /// Huffman code for n symbols, construct the tables required to decode those
        /// codes.  Those tables are the number of codes of each length, and the symbols
        /// sorted by length, retaining their original order within each length.  The
        /// return value is zero for a complete code set, negative for an over-
        /// subscribed code set, and positive for an incomplete code set.  The tables
        /// can be used if the return value is zero or positive, but they cannot be used
        /// if the return value is negative.  If the return value is zero, it is not
        /// possible for decode() using that table to return an error--any stream of
        /// enough bits will resolve to a symbol.  If the return value is positive, then
        /// it is possible for decode() using that table to return an error for received
        /// codes past the end of the incomplete lengths.
        /// </summary>
        /// <param name="rep">Repeated code length array</param>
        public int Initialize(byte[] rep)
        {
            int n = rep.Length; // Length of the bit length array
            short symbol = 0;   // Current symbol when stepping through length[]
            short len;          // Current length when stepping through h.Count[]
            int left;           // Number of possible codes left of current length
            short[] offs = new short[Constants.MAXBITS + 1];      // offsets in symbol table for each length
            short[] length = new short[256];  // Code lengths

            // Convert compact repeat counts into symbol bit length list
            int repPtr = 0;
            do
            {
                len = rep[repPtr++];
                left = (len >> 4) + 1;
                len &= 15;
                do
                {
                    length[symbol++] = len;
                }
                while (--left != 0);
            }
            while (--n != 0);

            n = symbol;

            // Count number of codes of each length
            for (len = 0; len <= Constants.MAXBITS; len++)
            {
                Count[len] = 0;
            }

            // Assumes lengths are within bounds
            for (symbol = 0; symbol < n; symbol++)
            {
                (Count[length[symbol]])++;
            }

            // No codes! Complete, but decode() will fail
            if (Count[0] == n)
                return 0;

            // Check for an over-subscribed or incomplete set of lengths
            left = 1; // One possible code of zero length
            for (len = 1; len <= Constants.MAXBITS; len++)
            {
                left <<= 1;             // One more bit, double codes left
                left -= Count[len];   // Deduct count from possible codes
                if (left < 0)
                    return left;        // over-subscribed--return negative
            }

            // Generate offsets into symbol table for each length for sorting
            offs[1] = 0;
            for (len = 1; len < Constants.MAXBITS; len++)
            {
                offs[len + 1] = (short)(offs[len] + Count[len]);
            }

            // Put symbols in table sorted by length, by symbol order within each length
            for (symbol = 0; symbol < n; symbol++)
            {
                if (length[symbol] != 0)
                    Symbol[offs[length[symbol]]++] = symbol;
            }

            // Return zero for complete set, positive for incomplete set
            return left;
        }

        /// <summary>
        /// Decode a code from the stream s using huffman table h.  Return the symbol or
        /// a negative value if there is an error.  If all of the lengths are zero, i.e.
        /// an empty code, or if the code is incomplete and an invalid code is received,
        /// then -9 is returned after reading MAXBITS bits.
        /// </summary>
        /// <param name="state">Current input/output state to process</param>
        /// <remarks>
        /// The codes as stored in the compressed data are bit-reversed relative to
        /// a simple integer ordering of codes of the same lengths.  Hence below the
        /// bits are pulled from the compressed data one at a time and used to
        /// build the code value reversed from what is in the stream in order to
        /// permit simple integer comparisons for decoding.
        /// 
        /// The first code for the shortest length is all ones.  Subsequent codes of
        /// the same length are simply integer decrements of the previous code.  When
        /// moving up a length, a one bit is appended to the code.  For a complete
        /// code, the last code of the longest length will be all zeros.  To support
        /// this ordering, the bits pulled during decoding are inverted to apply the
        /// more "natural" ordering starting with all zeros and incrementing.
        /// </remarks>
        public int Decode(State state)
        {
            int len = 1;                // Current number of bits in code
            int code = 0;               // len bits being decoded
            int first = 0;              // First code of length len
            int count;                  // Number of codes of length len
            int index = 0;              // Index of first code of length len in symbol table
            int bitbuf = state.BitBuf;  // Bits from stream
            int left = state.BitCnt;    // Bits left in next or left to process
            int nextPtr = CountPtr + 1; // Next number of codes

            while (true)
            {
                while (left-- != 0)
                {
                    // Invert code
                    code |= (bitbuf & 1) ^ 1;
                    bitbuf >>= 1;
                    count = Count[nextPtr++];

                    // If length len, return symbol
                    if (code < first + count)
                    {
                        state.BitBuf = bitbuf;
                        state.BitCnt = (state.BitCnt - len) & 7;
                        return Symbol[index + (code - first)];
                    }

                    // Else update for next length
                    index += count;
                    first += count;
                    first <<= 1;
                    code <<= 1;
                    len++;
                }

                left = (Constants.MAXBITS + 1) - len;
                if (left == 0)
                    break;

                if (state.Left == 0)
                {
                    state.Left = state.ProcessInput();
                    if (state.Left == 0)
                        throw new IndexOutOfRangeException();
                }

                bitbuf = state.InputPtr++;
                state.Left--;
                if (left > 8)
                    left = 8;
            }

            // Ran out of codes
            return -9;
        }
    };
}