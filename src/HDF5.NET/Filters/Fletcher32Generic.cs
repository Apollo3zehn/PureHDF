using System;

namespace HDF5.NET
{
    public static class Fletcher32Generic
    {
        public static unsafe uint Fletcher32(Span<byte> data) 
        {
            // H5checksum.c (H5_checksum_fletcher32)

            var len = (ulong)data.Length / 2;
            var sum1 = 0U;
            var sum2 = 0U;

            /* Compute checksum for pairs of bytes */
            /* (the magic "360" value is is the largest number of sums that can be
             *  performed without numeric overflow)
             */
            fixed (byte* dataPtr = data)
            {
                var dataPtrCpy = dataPtr;

                while (len != 0)
                {
                    var tlen = len > 360 ? 360 : len;

                    len -= tlen;

                    do
                    {
                        sum1 += (uint)((dataPtrCpy[0]) << 8) | (dataPtrCpy[1]);
                        dataPtrCpy += 2;
                        sum2 += sum1;
                    } while (--tlen != 0);

                    sum1 = (sum1 & 0xffff) + (sum1 >> 16);
                    sum2 = (sum2 & 0xffff) + (sum2 >> 16);
                }

                /* Check for odd # of bytes */
                if (data.Length % 2 != 0)
                {
                    sum1 += (uint)(*dataPtrCpy << 8);
                    sum2 += sum1;
                    sum1 = (sum1 & 0xffff) + (sum1 >> 16);
                    sum2 = (sum2 & 0xffff) + (sum2 >> 16);
                } /* end if */

                /* Second reduction step to reduce sums to 16 bits */
                sum1 = (sum1 & 0xffff) + (sum1 >> 16);
                sum2 = (sum2 & 0xffff) + (sum2 >> 16);

                return (sum2 << 16) | sum1;
            }
        }
    }
}
