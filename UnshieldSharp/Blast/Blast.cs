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

        /// <summary>
        /// Literal code
        /// </summary>
        private static readonly Huffman litcode = new Huffman(Constants.MAXBITS + 1, 256);

        /// <summary>
        /// Length code
        /// </summary>
        private static readonly Huffman lencode = new Huffman(Constants.MAXBITS + 1, 16);

        /// <summary>
        /// Distance code
        /// </summary>
        private static readonly Huffman distcode = new Huffman(Constants.MAXBITS + 1, 64);

        /// <summary>
        /// Base for length codes
        /// </summary>
        private static readonly short[] baseLength = new short[16]
        {
            3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264
        };

        /// <summary>
        /// Extra bits for length codes
        /// </summary>
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
            // Repeated code lengths of literal codes
            byte[] litlen = new byte[]
            {
                11, 124, 8, 7, 28, 7, 188, 13, 76, 4, 10, 8, 12, 10, 12, 10, 8, 23, 8,
                9, 7, 6, 7, 8, 7, 6, 55, 8, 23, 24, 12, 11, 7, 9, 11, 12, 6, 7, 22, 5,
                7, 24, 6, 11, 9, 6, 7, 22, 7, 11, 38, 7, 9, 8, 25, 11, 8, 11, 9, 12,
                8, 12, 5, 38, 5, 38, 5, 11, 7, 5, 6, 21, 6, 10, 53, 8, 7, 24, 10, 27,
                44, 253, 253, 253, 252, 252, 252, 13, 12, 45, 12, 45, 12, 61, 12, 45,
                44, 173
            };
            litcode.Initialize(litlen);

            // Repeated code lengths of length codes 0..15
            byte[] lenlen = new byte[]
            {
                2, 35, 36, 53, 38, 23
            };
            lencode.Initialize(lenlen);

            // Repeated code lengths of distance codes 0..63
            byte[] distlen = new byte[]
            {
                2, 20, 53, 230, 247, 151, 248
            };
            distcode.Initialize(distlen);
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
                Input = new List<byte>(),
                InputPtr = 0,
                Left = 0,
                BitBuf = 0,
                BitCnt = 0,

                OutHow = outhow,
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
            if (err != 1 && s.Next != 0 && !s.ProcessOutput() && err == 0)
                err = 1;

            return err;
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
            int symbol;         // decoded symbol, extra bits for distance
            int len;            // length for copy
            uint dist;          // distance for copy
            int copy;           // copy counter
            int from, to;       // copy pointers

            // Read header
            int lit = s.Bits(8); // true if literals are coded
            if (lit > 1)
                return -1;

            int dict = s.Bits(8); // log2(dictionary size) - 6
            if (dict < 4 || dict > 6)
                return -2;

            // Decode literals and length/distance pairs
            do
            {
                if (s.Bits(1) != 0)
                {
                    // Get length
                    symbol = lencode.Decode(s);
                    len = baseLength[symbol] + s.Bits(extra[symbol]);
                    if (len == 519)
                        break; // end code

                    // Get distance
                    symbol = len == 2 ? 2 : dict;
                    dist = (uint)(distcode.Decode(s) << symbol);
                    dist += (uint)s.Bits(symbol);
                    dist++;
                    if (s.First && dist > s.Next)
                        return -3; //distance too far back

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
                            if (!s.ProcessOutput())
                                return 1;

                            s.Next = 0;
                            s.First = false;
                        }
                    }
                    while (len != 0);
                }
                else
                {
                    // Get literal and write it
                    symbol = lit != 0 ? litcode.Decode(s) : s.Bits(8);
                    s.Output[s.Next++] = (byte)symbol;
                    if (s.Next == Constants.MAXWIN)
                    {
                        if (!s.ProcessOutput())
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