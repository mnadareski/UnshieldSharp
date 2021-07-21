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
        public int CountPtr;

        /// <summary>
        /// Canonically ordered symbols
        /// </summary>
        public short[] Symbol;
    };
}