/* blast.c
 * Copyright (C) 2003, 2012, 2013 Mark Adler
 * For conditions of distribution and use, see copyright notice in blast.h
 * version 1.3, 24 Aug 2013
 *
 * blast.c decompresses data compressed by the PKWare Compression Library.
 * This function provides functionality similar to the explode() function of
 * the PKWare library, hence the name "blast".
 *
 * This decompressor is based on the excellent format description provided by
 * Ben Rudiak-Gould in comp.compression on August 13, 2001.  Interestingly, the
 * example Ben provided in the post is incorrect.  The distance 110001 should
 * instead be 111000.  When corrected, the example byte stream becomes:
 *
 *    00 04 82 24 25 8f 80 7f
 *
 * which decompresses to "AIAIAIAIAIAIA" (without the quotes).
 */

/*
 * Change history:
 *
 * 1.0  12 Feb 2003     - First version
 * 1.1  16 Feb 2003     - Fixed distance check for > 4 GB uncompressed data
 * 1.2  24 Oct 2012     - Add note about using binary mode in stdio
 *                      - Fix comparisons of differently signed integers
 * 1.3  24 Aug 2013     - Return unused input from blast()
 *                      - Fix test code to correctly report unused input
 *                      - Enable the provision of initial input to blast()
 */

using System;
using System.Collections.Generic;

namespace UnshieldSharp.Blast
{
    public unsafe static class BlastDecoder
    {
        #region Huffman Encoding

        // Literal code
        private static readonly Huffman litcode = new Huffman
        {
            Count = new short[Constants.MAXBITS + 1],
            Symbol = new short[256],
        };

        // Length code
        private static readonly Huffman lencode = new Huffman
        {
            Count = new short[Constants.MAXBITS + 1],
            Symbol = new short[16],
        };

        // Distance code
        private static readonly Huffman distcode = new Huffman
        {
            Count = new short[Constants.MAXBITS + 1],
            Symbol = new short[64],
        };

        // Bit lengths of literal codes
        private static readonly byte[] litlen = new byte[]
        {
            11, 124, 8, 7, 28, 7, 188, 13, 76, 4, 10, 8, 12, 10, 12, 10, 8, 23, 8,
            9, 7, 6, 7, 8, 7, 6, 55, 8, 23, 24, 12, 11, 7, 9, 11, 12, 6, 7, 22, 5,
            7, 24, 6, 11, 9, 6, 7, 22, 7, 11, 38, 7, 9, 8, 25, 11, 8, 11, 9, 12,
            8, 12, 5, 38, 5, 38, 5, 11, 7, 5, 6, 21, 6, 10, 53, 8, 7, 24, 10, 27,
            44, 253, 253, 253, 252, 252, 252, 13, 12, 45, 12, 45, 12, 61, 12, 45,
            44, 173
        };

        // Bit lengths of length codes 0..15
        private static readonly byte[] lenlen = new byte[]
        {
            2, 35, 36, 53, 38, 23
        };

        // Bit lengths of distance codes 0..63
        private static readonly byte[] distlen = new byte[]
        {
            2, 20, 53, 230, 247, 151, 248
        };

        // Base for length codes
        private static readonly short[] baseLength = new short[16]
        {
            3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264
        };

        // Extra bits for length codes
        private static readonly byte[] extra = new byte[16]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8
        };

        #endregion

        /// <summary>
        /// Static constructor
        /// </summary>
        static BlastDecoder()
        {
            // Set up decoding tables (once--might not be thread-safe)
            Construct(litcode, litlen, litlen.Length);
            Construct(lencode, lenlen, lenlen.Length);
            Construct(distcode, distlen, distlen.Length);
        }

        /// <summary>
        /// blast() decompresses the PKWare Data Compression Library (DCL) compressed
        /// format.  It provides the same functionality as the explode() function in
        /// that library.  (Note: PKWare overused the "implode" verb, and the format
        /// used by their library implode() function is completely different and
        /// incompatible with the implode compression method supported by PKZIP.)
        /// 
        /// The binary mode for stdio functions should be used to assure that the
        /// compressed data is not corrupted when read or written.  For example:
        /// fopen(..., "rb") and fopen(..., "wb").
        /// </summary>
        public static int Blast(byte[] inhow, List<byte> outhow)
        {
            // Input/output state
            State s = new State
            {
                InHow = inhow,
                InHowPtr = 0,
                Input = new List<byte>(),
                InputPtr = 0,
                Left = 0,
                BitBuf = 0,
                BitCnt = 0,

                OutHow = outhow,
                OutHowPtr = 0,
                Next = 0,
                First = true,
            };

            // Attempt to decompress using the above state
            int err;
            try
            {
                err = Decomp(s);
            }
            catch (IndexOutOfRangeException)
            {
                // This was originally a jump, which is bad form for C#
                err = 2;
            }

            // Write any leftover output and update the error code if needed
            if (err != 1 && s.Next != 0 && s.ProcessOutput() != 0 && err == 0)
                err = 1;

            return err;
        }

        /// <summary>
        /// Return need bits from the input stream.  This always leaves less than
        /// eight bits in the buffer.  bits() works properly for need == 0.
        /// </summary>
        /// <remarks>
        /// Bits are stored in bytes from the least significant bit to the most
        /// significant bit.  Therefore bits are dropped from the bottom of the bit
        /// buffer, using shift right, and new bytes are appended to the top of the
        /// bit buffer, using shift left.
        /// </remarks>
        private static int Bits(State s, int need)
        {
            // Load at least need bits into val
            int val = s.BitBuf;
            while (s.BitCnt < need)
            {
                if (s.Left == 0)
                {
                    s.Left = s.ProcessInput();
                    if (s.Left == 0)
                        throw new IndexOutOfRangeException();
                }

                // Load eight bits
                val |= s.Input[s.InputPtr++] << s.BitCnt;
                s.Left--;
                s.BitCnt += 8;
            }

            // Drop need bits and update buffer, always zero to seven bits left
            s.BitBuf = val >> need;
            s.BitCnt -= need;

            // Return need bits, zeroing the bits above that
            return val & ((1 << need) - 1);
        }

        /// <summary>
        /// Decode a code from the stream s using huffman table h.  Return the symbol or
        /// a negative value if there is an error.  If all of the lengths are zero, i.e.
        /// an empty code, or if the code is incomplete and an invalid code is received,
        /// then -9 is returned after reading MAXBITS bits.
        /// </summary>
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
        private static int Decode(State s, Huffman h)
        {
            int len = 1;                    /* current number of bits in code */
            int code = 0;                   /* len bits being decoded */
            int first = 0;                  /* first code of length len */
            int count;                      /* number of codes of length len */
            int index = 0;                  /* index of first code of length len in symbol table */
            int bitbuf = s.BitBuf;          /* bits from stream */
            int left = s.BitCnt;            /* bits left in next or left to process */
            int nextPtr = h.CountPtr + 1;   /* next number of codes */

            while (true)
            {
                while (left-- != 0)
                {
                    // Invert code
                    code |= (bitbuf & 1) ^ 1;
                    bitbuf >>= 1;
                    count = h.Count[nextPtr++];

                    // If length len, return symbol
                    if (code < first + count)
                    {
                        s.BitBuf = bitbuf;
                        s.BitCnt = (s.BitCnt - len) & 7;
                        return h.Symbol[index + (code - first)];
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
                    
                if (s.Left == 0)
                {
                    s.Left = s.ProcessInput();
                    if (s.Left == 0)
                        throw new IndexOutOfRangeException(); /* out of input */
                }

                bitbuf = s.InputPtr++;
                s.Left--;
                if (left > 8)
                    left = 8;
            }

            return -9;                          /* ran out of codes */
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
        private static int Construct(Huffman h, byte[] rep, int n)
        {
            short symbol = 0;   /* current symbol when stepping through length[] */
            short len;          /* current length when stepping through h.Count[] */
            int left;           /* number of possible codes left of current length */
            short[] offs = new short[Constants.MAXBITS+1];      /* offsets in symbol table for each length */
            short[] length = new short[256];  /* code lengths */

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
                h.Count[len] = 0;
            }

            // Assumes lengths are within bounds
            for (symbol = 0; symbol < n; symbol++)
            {
                (h.Count[length[symbol]])++;
            }
            
            // No codes! Complete, but decode() will fail
            if (h.Count[0] == n)
                return 0;

            // Check for an over-subscribed or incomplete set of lengths
            left = 1;                           /* one possible code of zero length */
            for (len = 1; len <= Constants.MAXBITS; len++)
            {
                left <<= 1;                     /* one more bit, double codes left */
                left -= h.Count[len];          /* deduct count from possible codes */
                if (left < 0)
                    return left;      /* over-subscribed--return negative */
            }                                   /* left > 0 means incomplete */

            // Generate offsets into symbol table for each length for sorting
            offs[1] = 0;
            for (len = 1; len < Constants.MAXBITS; len++)
            {
                offs[len + 1] = (short)(offs[len] + h.Count[len]);
            }

            // Put symbols in table sorted by length, by symbol order within each length
            for (symbol = 0; symbol < n; symbol++)
            {
                if (length[symbol] != 0)
                    h.Symbol[offs[length[symbol]]++] = symbol;
            }

            // Return zero for complete set, positive for incomplete set
            return left;
        }

        /// <summary>
        /// Decode PKWare Compression Library stream.
        /// </summary>
        /// <remarks>
        /// First byte is 0 if literals are uncoded or 1 if they are coded.  Second
        /// byte is 4, 5, or 6 for the number of extra bits in the distance code.
        /// This is the base-2 logarithm of the dictionary size minus six.
        /// 
        /// Compressed data is a combination of literals and length/distance pairs
        /// terminated by an end code.  Literals are either Huffman coded or
        /// uncoded bytes.  A length/distance pair is a coded length followed by a
        /// coded distance to represent a string that occurs earlier in the
        /// uncompressed data that occurs again at the current location.
        /// 
        /// A bit preceding a literal or length/distance pair indicates which comes
        /// next, 0 for literals, 1 for length/distance.
        /// 
        /// If literals are uncoded, then the next eight bits are the literal, in the
        /// normal bit order in the stream, i.e. no bit-reversal is needed. Similarly,
        /// no bit reversal is needed for either the length extra bits or the distance
        /// extra bits.
        /// 
        /// Literal bytes are simply written to the output.  A length/distance pair is
        /// an instruction to copy previously uncompressed bytes to the output.  The
        /// copy is from distance bytes back in the output stream, copying for length
        /// bytes.
        /// 
        /// Distances pointing before the beginning of the output data are not
        /// permitted.
        /// 
        /// Overlapped copies, where the length is greater than the distance, are
        /// allowed and common.  For example, a distance of one and a length of 518
        /// simply copies the last byte 518 times.  A distance of four and a length of
        /// twelve copies the last four bytes three times.  A simple forward copy
        /// ignoring whether the length is greater than the distance or not implements
        /// this correctly.
        /// </remarks>
        private static int Decomp(State s)
        {
            int lit;            /* true if literals are coded */
            int dict;           /* log2(dictionary size) - 6 */
            int symbol;         /* decoded symbol, extra bits for distance */
            int len;            /* length for copy */
            uint dist;          /* distance for copy */
            int copy;           /* copy counter */
            int from, to;       /* copy pointers */

            // Read header
            lit = Bits(s, 8);
            if (lit > 1)
                return -1;

            dict = Bits(s, 8);
            if (dict < 4 || dict > 6)
                return -2;

            // Decode literals and length/distance pairs
            do
            {
                if (Bits(s, 1) != 0)
                {
                    // Get length
                    symbol = Decode(s, lencode);
                    len = baseLength[symbol] + Bits(s, extra[symbol]);
                    if (len == 519)
                        break;              /* end code */

                    // Get distance
                    symbol = len == 2 ? 2 : dict;
                    dist = (uint)(Decode(s, distcode) << symbol);
                    dist += (uint)Bits(s, symbol);
                    dist++;
                    if (s.First && dist > s.Next)
                        return -3;              /* distance too far back */

                    // Copy length bytes from distance bytes back
                    do
                    {
                        to = (int)(s.OutputPtr + s.Next);
                        from = (int)(to - dist);
                        copy = Constants.MAXWIN;
                        if (s.Next < dist)
                        {
                            from += copy;
                            copy = (int)dist;
                        }

                        copy -= (int)s.Next;
                        if (copy > len)
                            copy = len;

                        len -= copy;
                        s.Next += (uint)copy;
                        do
                        {
                            s.Output[to++] = s.Output[from++];
                        }
                        while (--copy != 0);

                        if (s.Next == Constants.MAXWIN)
                        {
                            if (s.ProcessOutput() != 0)
                                return 1;

                            s.Next = 0;
                            s.First = false;
                        }
                    }
                    while (len != 0);
                }
                else
                {
                    /* get literal and write it */
                    symbol = lit != 0 ? Decode(s, litcode) : Bits(s, 8);
                    s.Output[s.Next++] = (byte)symbol;
                    if (s.Next == Constants.MAXWIN)
                    {
                        if (s.ProcessOutput() != 0)
                            return 1;
                        
                        s.Next = 0;
                        s.First = false;
                    }
                }
            }
            while (true);

            return 0;
        }
    }
}