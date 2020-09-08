using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace HDF5.NET
{
    internal static class H5Checksum
    {
        public static uint JenkinsLookup3(string key)
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            return H5Checksum.JenkinsLookup3Internal(bytes, 0);
        }

        /*
        -------------------------------------------------------------------------------
        H5_lookup3_mix -- mix 3 32-bit values reversibly.

        This is reversible, so any information in (a,b,c) before mix() is
        still in (a,b,c) after mix().

        If four pairs of (a,b,c) inputs are run through mix(), or through
        mix() in reverse, there are at least 32 bits of the output that
        are sometimes the same for one pair and different for another pair.
        This was tested for:
        * pairs that differed by one bit, by two bits, in any combination
          of top bits of (a,b,c), or in any combination of bottom bits of
          (a,b,c).
        * "differ" is defined as +, -, ^, or ~^.  For + and -, I transformed
          the output delta to a Gray code (a^(a>>1)) so a string of 1's (as
          is commonly produced by subtraction) look like a single 1-bit
          difference.
        * the base values were pseudorandom, all zero but one bit set, or
          all zero plus a counter that starts at zero.

        Some k values for my "a-=c; a^=rot(c,k); c+=b;" arrangement that
        satisfy this are
            4  6  8 16 19  4
            9 15  3 18 27 15
           14  9  3  7 17  3
        Well, "9 15 3 18 27 15" didn't quite get 32 bits diffing
        for "differ" defined as + with a one-bit base and a two-bit delta.  I
        used http://burtleburtle.net/bob/hash/avalanche.html to choose
        the operations, constants, and arrangements of the variables.

        This does not achieve avalanche.  There are input bits of (a,b,c)
        that fail to affect some output bits of (a,b,c), especially of a.  The
        most thoroughly mixed value is c, but it doesn't really even achieve
        avalanche in c.

        This allows some parallelism.  Read-after-writes are good at doubling
        the number of bits affected, so the goal of mixing pulls in the opposite
        direction as the goal of parallelism.  I did what I could.  Rotates
        seem to cost as much as shifts on every machine I could lay my hands
        on, and rotates are much kinder to the top and bottom bits, so I used
        rotates.
        -------------------------------------------------------------------------------
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint JenkinsLookup3Rot(uint x, int k)
        {
            return (x << k) ^ (x >> (32 - k));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void JenkinsLookup3Mix(ref uint a, ref uint b, ref uint c)
        {
            a -= c; a ^= H5Checksum.JenkinsLookup3Rot(c, 4);    c += b;
            b -= a; b ^= H5Checksum.JenkinsLookup3Rot(a, 6);    a += c;
            c -= b; c ^= H5Checksum.JenkinsLookup3Rot(b, 8);    b += a;
            a -= c; a ^= H5Checksum.JenkinsLookup3Rot(c, 16);   c += b;
            b -= a; b ^= H5Checksum.JenkinsLookup3Rot(a, 19);   a += c;
            c -= b; c ^= H5Checksum.JenkinsLookup3Rot(b, 4);    b += a;
        }

        /*
        -------------------------------------------------------------------------------
        H5_lookup3_final -- final mixing of 3 32-bit values (a,b,c) into c

        Pairs of (a,b,c) values differing in only a few bits will usually
        produce values of c that look totally different.  This was tested for
        * pairs that differed by one bit, by two bits, in any combination
          of top bits of (a,b,c), or in any combination of bottom bits of
          (a,b,c).
        * "differ" is defined as +, -, ^, or ~^.  For + and -, I transformed
          the output delta to a Gray code (a^(a>>1)) so a string of 1's (as
          is commonly produced by subtraction) look like a single 1-bit
          difference.
        * the base values were pseudorandom, all zero but one bit set, or
          all zero plus a counter that starts at zero.

        These constants passed:
         14 11 25 16 4 14 24
         12 14 25 16 4 14 24
        and these came close:
          4  8 15 26 3 22 24
         10  8 15 26 3 22 24
         11  8 15 26 3 22 24
        -------------------------------------------------------------------------------
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void JenkinsLookup3Final(ref uint a, ref uint b, ref uint c)
        {
            c ^= b; c -= H5Checksum.JenkinsLookup3Rot(b, 14);
            a ^= c; a -= H5Checksum.JenkinsLookup3Rot(c, 11);
            b ^= a; b -= H5Checksum.JenkinsLookup3Rot(a, 25);
            c ^= b; c -= H5Checksum.JenkinsLookup3Rot(b, 16);
            a ^= c; a -= H5Checksum.JenkinsLookup3Rot(c, 4);
            b ^= a; b -= H5Checksum.JenkinsLookup3Rot(a, 14);
            c ^= b; c -= H5Checksum.JenkinsLookup3Rot(b, 24);
        }

        /*
        -------------------------------------------------------------------------------
        H5_checksum_lookup3() -- hash a variable-length key into a 32-bit value
          k       : the key (the unaligned variable-length array of bytes)
          length  : the length of the key, counting by bytes
          initval : can be any 4-byte value
        Returns a 32-bit value.  Every bit of the key affects every bit of
        the return value.  Two keys differing by one or two bits will have
        totally different hash values.

        The best hash table sizes are powers of 2.  There is no need to do
        mod a prime (mod is sooo slow!).  If you need less than 32 bits,
        use a bitmask.  For example, if you need only 10 bits, do
          h = (h & hashmask(10));
        In which case, the hash table should have hashsize(10) elements.

        If you are hashing n strings (uint8_t **)k, do it like this:
          for (i=0, h=0; i<n; ++i) h = H5_checksum_lookup( k[i], len[i], h);

        By Bob Jenkins, 2006.  bob_jenkins@burtleburtle.net.  You may use this
        code any way you wish, private, educational, or commercial.  It's free.

        Use for hash table lookup, or anything where one collision in 2^^32 is
        acceptable.  Do NOT use for cryptographic purposes.
        -------------------------------------------------------------------------------
        */
        private static unsafe uint JenkinsLookup3Internal(byte[] bytes, uint initialValue)
        {
            uint a, b, c;

            /* Set up the internal state */
            var length = (uint)bytes.Length;
            a = b = c = 0xdeadbeef + length + initialValue;

            fixed (byte* p = bytes)
            {
                var k = p;

                /*--------------- all but the last block: affect some 32 bits of (a,b,c) */
                while (length > 12)
                {
                    a += k[0];
                    a += ((uint)k[1]) << 8;
                    a += ((uint)k[2]) << 16;
                    a += ((uint)k[3]) << 24;
                    b += k[4];
                    b += ((uint)k[5]) << 8;
                    b += ((uint)k[6]) << 16;
                    b += ((uint)k[7]) << 24;
                    c += k[8];
                    c += ((uint)k[9]) << 8;
                    c += ((uint)k[10]) << 16;
                    c += ((uint)k[11]) << 24;
                    H5Checksum.JenkinsLookup3Mix(ref a, ref b, ref c);
                    length -= 12;
                    k += 12;
                }

                /*-------------------------------- last block: affect all 32 bits of (c) */
                switch (length)                   /* all the case statements fall through */
                {
                    case 12:    c += ((uint)k[11]) << 24;   goto case 11;
                    case 11:    c += ((uint)k[10]) << 16;   goto case 10;
                    case 10:    c += ((uint)k[9]) << 8;     goto case 9;
                    case 9:     c += k[8];                  goto case 8;
                    case 8:     b += ((uint)k[7]) << 24;    goto case 7;
                    case 7:     b += ((uint)k[6]) << 16;    goto case 6;
                    case 6:     b += ((uint)k[5]) << 8;     goto case 5;
                    case 5:     b += k[4];                  goto case 4;
                    case 4:     a += ((uint)k[3]) << 24;    goto case 3;
                    case 3:     a += ((uint)k[2]) << 16;    goto case 2;
                    case 2:     a += ((uint)k[1]) << 8;     goto case 1;
                    case 1:     a += k[0];                  break;
                    case 0:                                 return c;
                    default:
                        throw new Exception("This Should never be executed!");
                }

                H5Checksum.JenkinsLookup3Final(ref a, ref b, ref c);

                return c;
            }
        }
    }
}
