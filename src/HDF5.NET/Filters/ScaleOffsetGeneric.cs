using System;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    internal static class ScaleOffsetGeneric
    {
        private enum H5Z_SO_scale_type
        {
            FLOAT_DSCALE = 0,
            FLOAT_ESCALE = 1,
            INT = 2
        }

        private struct parms_atomic
        {
            public uint size;               /* datatype size */
            public uint minbits;            /* minimum bits to compress one value of such datatype */
            public bool is_little_endian;   /* current memory endianness order */
        }

        private enum H5Z_scaleoffset
        {
            t_bad = 0,
            t_uchar = 1,
            t_ushort,
            t_uint,
            t_ulong,
            t_schar,
            t_short,
            t_int,
            t_long,
            t_long_long,
            t_float,
            t_double
        };

        private const int H5Z_SCALEOFFSET_TOTAL_NPARMS = 20;    /* Total number of parameters for filter */
        private const int H5Z_SCALEOFFSET_PARM_SCALETYPE = 0;   /* "User" parameter for scale type */
        private const int H5Z_SCALEOFFSET_PARM_SCALEFACTOR = 1; /* "User" parameter for scale factor */
        private const int H5Z_SCALEOFFSET_PARM_NELMTS = 2;      /* "Local" parameter for number of elements in the chunk */
        private const int H5Z_SCALEOFFSET_PARM_CLASS = 3;       /* "Local" parameter for datatype class */
        private const int H5Z_SCALEOFFSET_PARM_SIZE = 4;        /* "Local" parameter for datatype size */
        private const int H5Z_SCALEOFFSET_PARM_SIGN = 5 ;       /* "Local" parameter for integer datatype sign */
        private const int H5Z_SCALEOFFSET_PARM_ORDER = 6;       /* "Local" parameter for datatype byte order */
        private const int H5Z_SCALEOFFSET_PARM_FILAVAIL = 7;    /* "Local" parameter for dataset fill value existence */

        private const int H5Z_SCALEOFFSET_CLS_INTEGER = 0;      /* Integer (datatype class) */
        private const int H5Z_SCALEOFFSET_CLS_FLOAT = 1;        /* Floatig-point (datatype class) */

        private const int H5Z_SCALEOFFSET_SGN_NONE = 0;         /* Unsigned integer type */
        private const int H5Z_SCALEOFFSET_SGN_2 = 1;            /* Two's complement signed integer type */

        private const int H5Z_SCALEOFFSET_ORDER_LE = 0;         /* Little endian (datatype byte order) */
        private const int H5Z_SCALEOFFSET_ORDER_BE = 1;         /* Big endian (datatype byte order) */

        private const int H5Z_SCALEOFFSET_FILL_UNDEFINED = 0;   /* Fill value is not defined */
        private const int H5Z_SCALEOFFSET_FILL_DEFINED = 1;     /* Fill value is defined */

        public static Memory<byte> ScaleOffset(uint[] parameters, Memory<byte> data) 
        {
            // H5Zscaleoffset.c (H5Z__filter_scaleoffset)

            /* check arguments */
            if (parameters.Length != H5Z_SCALEOFFSET_TOTAL_NPARMS)
                throw new Exception("Invalid scaleoffset number of parameters.");

            /* Check if memory byte order matches dataset datatype byte order */
            bool need_convert = false;

            if (BitConverter.IsLittleEndian)
            {
                if (parameters[H5Z_SCALEOFFSET_PARM_ORDER] == H5Z_SCALEOFFSET_ORDER_BE)
                    need_convert = true;
            }
            else
            {
                if (parameters[H5Z_SCALEOFFSET_PARM_ORDER] == H5Z_SCALEOFFSET_ORDER_LE)
                    need_convert = true;
            }

            /* copy filter parameters to local variables */
            uint d_nelmts = parameters[H5Z_SCALEOFFSET_PARM_NELMTS];
            uint dtype_class = parameters[H5Z_SCALEOFFSET_PARM_CLASS];
            uint dtype_sign = parameters[H5Z_SCALEOFFSET_PARM_SIGN];
            bool filavail = parameters[H5Z_SCALEOFFSET_PARM_FILAVAIL] == H5Z_SCALEOFFSET_FILL_DEFINED;
            int scale_factor = (int)parameters[H5Z_SCALEOFFSET_PARM_SCALEFACTOR];
            H5Z_SO_scale_type scale_type = (H5Z_SO_scale_type)parameters[H5Z_SCALEOFFSET_PARM_SCALETYPE];

            /* check and assign proper values set by user to related parameters
             * scale type can be H5Z_SO_FLOAT_DSCALE (0), H5Z_SO_FLOAT_ESCALE (1) or H5Z_SO_INT (other)
             * H5Z_SO_FLOAT_DSCALE : floating-point type, variable-minimum-bits method,
             *                      scale factor is decimal scale factor
             * H5Z_SO_FLOAT_ESCALE : floating-point type, fixed-minimum-bits method,
             *                      scale factor is the fixed minimum number of bits
             * H5Z_SO_INT          : integer type, scale_factor is minimum number of bits
             */

            if (dtype_class == H5Z_SCALEOFFSET_CLS_FLOAT) /* floating-point type */
            {
                if (scale_type != H5Z_SO_scale_type.FLOAT_DSCALE && scale_type != H5Z_SO_scale_type.FLOAT_ESCALE)
                    throw new Exception("invalid scale type");
            }

            if (dtype_class == H5Z_SCALEOFFSET_CLS_INTEGER) /* integer type */
            {
                if (scale_type != H5Z_SO_scale_type.INT)
                    throw new Exception("invalid scale type");

                /* if scale_factor is less than 0 for integer, library will reset it to 0
                 * in this case, library will calculate the minimum-bits
                 */
                if (scale_factor < 0)
                    scale_factor = 0;
            }

            /* fixed-minimum-bits method is not implemented and is forbidden */
            if (scale_type == H5Z_SO_scale_type.FLOAT_ESCALE)
                throw new Exception("E-scaling method not supported.");

            double D_val;
            uint minbits;

            if (scale_type == H5Z_SO_scale_type.FLOAT_DSCALE) /* floating-point type, variable-minimum-bits */
            {
                D_val = scale_factor;
            }
            else /* integer type, or floating-point type with fixed-minimum-bits method */
            {
                if (scale_factor > (int)(parameters[H5Z_SCALEOFFSET_PARM_SIZE] * 8))
                    throw new Exception("Minimum number of bits exceeds maximum.");
        
                /* no need to process data */
                if (scale_factor == (int)(parameters[H5Z_SCALEOFFSET_PARM_SIZE] * 8))
                    return data;

                minbits = (uint)scale_factor;
            }

            /* prepare parameters to pass to compress/decompress functions */
            parms_atomic p = default;
            p.size = parameters[H5Z_SCALEOFFSET_PARM_SIZE];
            p.is_little_endian = BitConverter.IsLittleEndian;

            /* input; decompress */

            /* retrieve values of minbits and minval from input compressed buffer
             * retrieve them corresponding to how they are stored during compression
             */

            minbits = 0;

            var spanData = data.Span;

            for (int i = 0; i < 4; i++) {
                uint minbits_mask = spanData[i];
                minbits_mask <<= i * 8;
                minbits |= minbits_mask;
            }

            /* retrieval of minval takes into consideration situation where sizeof
             * unsigned long long (datatype of minval) may change from compression
             * to decompression, only smaller size is used
             */
            uint minval_size = spanData[4]; /* simplified */

            ulong minval = 0;

            for (int i = 0; i < minval_size; i++) {
                ulong minval_mask = spanData[5 + i];
                minval_mask <<= i * 8;
                minval |= minval_mask;
            }

            if (minbits > p.size * 8)
                throw new Exception("minbits > p.size * 8");

            /* calculate size of output buffer after decompression */
            int size_out = (int)(d_nelmts * p.size);

            /* allocate memory space for decompressed buffer */
            var outbuf = new byte[size_out];

            /* special case: minbits equal to full precision */
            var buf_offset = 21; /* buffer offset because of parameters stored in file */

            if (minbits == p.size * 8)
            {
                spanData
                    .Slice(buf_offset, size_out)
                    .CopyTo(outbuf);
                
                /* convert to dataset datatype endianness order if needed */
                if (need_convert)
                    H5Z__scaleoffset_convert(outbuf, d_nelmts, p.size);

                return outbuf;
            }

            /* decompress the buffer if minbits not equal to zero */
            if (minbits != 0)
                H5Z__scaleoffset_decompress(outbuf, spanData.Slice(buf_offset), p);

            else /* fill value is not defined and all data elements have the same value */
                outbuf.AsSpan().Fill(0);

            /* before postprocess, get memory type */
            var type = H5Z__scaleoffset_get_type(dtype_class, p.size, dtype_sign);

            /* postprocess after decompression */
            if (dtype_class == H5Z_SCALEOFFSET_CLS_INTEGER)
                H5Z__scaleoffset_postdecompress_i(outbuf, type, filavail, parameters, minbits, minval);

            if (dtype_class == H5Z_SCALEOFFSET_CLS_FLOAT)
            {
                if (scale_type == 0) /* variable-minimum-bits method */
                {
                    if (H5Z__scaleoffset_postdecompress_fd(outbuf, type, filavail, parameters, minbits,
                                                           minval, D_val) == FAIL)
                        throw new Exception("Post-decompression failed.");
                }
            }

            /* after postprocess, convert to dataset datatype endianness order if needed */
            if (need_convert)
                H5Z__scaleoffset_convert(outbuf, d_nelmts, p.size);

            return outbuf;
        }

        #region Algorithm

        /* ============ Scaleoffset Algorithm ===============================================
         * assume one byte has 8 bit
         * assume padding bit is 0
         * assume size of unsigned char is one byte
         * assume one data item of certain datatype is stored continuously in bytes
         * atomic datatype is treated on byte basis
         */

        /* change byte order of input buffer either from little-endian to big-endian
         * or from big-endian to little-endian  2/21/2005
         */
        private static void H5Z__scaleoffset_convert(Span<byte> buffer, uint d_nelmts, uint dtype_size)
        {
            var int_dtype_size = (int)dtype_size;

            if (dtype_size > 1)
            {
                for (int i = 0; i < d_nelmts * dtype_size; i += int_dtype_size)
                {
                    for (int j = 0; j < dtype_size / 2; j++)
                    {
                        /* swap pair of bytes */
                        var temp = buffer[i + j];
                        buffer[i + j] = buffer[i + int_dtype_size - 1 - j];
                        buffer[i + int_dtype_size - 1 - j] = temp;
                    }
                }
            }
        }

        /* postdecompress for integer type */
        static void
        H5Z__scaleoffset_postdecompress_i(
            Span<byte> data, H5Z_scaleoffset type,
            bool filavail, uint[] parameters, uint minbits, ulong minval)
        {
            long sminval = unchecked((long)minval); /* for signed integer types */

            if (type == H5Z_scaleoffset.t_uchar)
                H5Z_scaleoffset_postdecompress_1(data, filavail, parameters, minbits, minval);

            else if (type == H5Z_scaleoffset.t_ushort)
                H5Z_scaleoffset_postdecompress_1(MemoryMarshal.Cast<byte, ushort>(data), filavail, parameters, minbits, minval);

            else if (type == H5Z_scaleoffset.t_uint)
                H5Z_scaleoffset_postdecompress_1(MemoryMarshal.Cast<byte, uint>(data), filavail, parameters, minbits, minval);

            else if (type == H5Z_scaleoffset.t_ulong)
                H5Z_scaleoffset_postdecompress_1(MemoryMarshal.Cast<byte, ulong>(data), filavail, parameters, minbits, minval);

            else if (type == H5Z_scaleoffset.t_schar) 
            {
                sbyte* buf = (sbyte*)data, filval = 0;
                uint i;

                if (filavail) /* fill value defined */
                {
                    H5Z_scaleoffset_get_filval_1<sbyte>(parameters, filval);

                    for (i = 0; i < data.Length; i++) 
                        buf[i] = (sbyte)((buf[i] == (((byte)1 << minbits) - 1)) ? filval : (buf[i] + sminval));
                }

                else /* fill value undefined */
                {
                    for (i = 0; i < data.Length; i++)
                        buf[i] = (sbyte)(buf[i] + sminval);
                }
            }

            else if (type == H5Z_scaleoffset.t_short)
                H5Z_scaleoffset_postdecompress_2(MemoryMarshal.Cast<byte, short>(data), filavail, parameters, minbits, sminval);

            else if (type == H5Z_scaleoffset.t_int)
                H5Z_scaleoffset_postdecompress_2(MemoryMarshal.Cast<byte, int>(data), filavail, parameters, minbits, sminval);

            else if (type == H5Z_scaleoffset.t_long)
                H5Z_scaleoffset_postdecompress_2(MemoryMarshal.Cast<byte, long>(data), filavail, parameters, minbits, sminval);
        }

        private static void H5Z__scaleoffset_decompress_one_byte(
            Span<byte> data, int k, uint begin_i,
            Span<byte> buffer, ref int j, ref uint buf_len,
            parms_atomic p, uint dtype_len)
        {
            uint dat_len; /* dat_len is the number of bits to be copied in each data byte */
            byte val;     /* value to be copied in each data byte */

            /* initialize value and bits of unsigned char to be copied */
            val = buffer[j];

            if (k == begin_i)
                dat_len = 8 - (dtype_len - p.minbits) % 8;

            else
                dat_len = 8;

            if (buffer.Length > dat_len) 
            {
                data[k] = (byte)((uint)(val >> (int)(buf_len - dat_len)) & (~(uint.MaxValue << (int)dat_len)));
                buf_len -= dat_len;
            }

            else 
            {
                data[k] = (byte)((val & ~(uint.MaxValue << (int)buf_len)) << (int)(dat_len - buf_len));
                dat_len -= buf_len;

                // H5Z__scaleoffset_next_byte
                ++j;
                buf_len = 8 * sizeof(byte);

                if (dat_len == 0)
                    return;

                val = buffer[j];
                data[k] |= (byte)((uint)(val >> (int)(buf_len - dat_len)) & ~(uint.MaxValue << (int)dat_len));
                buf_len -= dat_len;
            } 
        }

        private static void H5Z__scaleoffset_decompress_one_atomic(
            Span<byte> data, Span<byte> buffer, ref int j, ref uint buf_len, parms_atomic p)
        {
            /* begin_i: the index of byte having first significant bit */
            uint begin_i;

            if (p.minbits <= 0)
                throw new Exception("p.minbits <= 0");

            uint dtype_len = p.size * 8;

            if (p.is_little_endian) /* little endian */
            { 
                begin_i = p.size - 1 - (dtype_len - p.minbits) / 8;

                for (int k = (int)begin_i; k >= 0; k--)
                    H5Z__scaleoffset_decompress_one_byte(data, k, begin_i, buffer, ref j, ref buf_len, p, dtype_len);
            }
            else /* big endian */
            { 
                if (!p.is_little_endian)
                    throw new Exception("p.mem_order != H5Z_SCALEOFFSET_ORDER_BE");

                begin_i = (dtype_len - p.minbits) / 8;

                for (int k = (int)begin_i; k <= (int)(p.size - 1); k++)
                    H5Z__scaleoffset_decompress_one_byte(data, k, begin_i, buffer, ref j, ref buf_len, p, dtype_len);
            }
        }

        private static void H5Z__scaleoffset_decompress(Span<byte> data, Span<byte> buffer, parms_atomic p)
        {
            /* i: index of data, j: index of buffer,
               buf_len: number of bits to be filled in current byte */
            /* must initialize to zeros */
            for (int i = 0; i < data.Length * p.size; i++)
                data[i] = 0;

            /* initialization before the loop */
            uint buf_len = sizeof(byte) * 8;

            /* decompress */
            int j = 0;

            for (int i = 0; i < data.Length; i++)
                H5Z__scaleoffset_decompress_one_atomic(data.Slice(i * (int)p.size), buffer.Slice(0, 1), ref j, ref buf_len, p);
        }

        #endregion

        #region Helpers

        private static void H5Z_scaleoffset_postdecompress_1<T>(Span<T> buf, bool filavail, uint[] parameters, uint minbits, ulong minval)
            where T : struct
        {
            do
            {
                T filval = 0;

                if (filavail) /* fill value defined */
                {
                    H5Z_scaleoffset_get_filval_1(type, parameters, filval);

                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = (type)((buf[i] == (((type)1 << minbits) - 1)) 
                            ? filval
                            : (buf[i] + minval));
                    }
                }

                else /* fill value undefined */
                {
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = (type)(buf[i] + (type)(minval));
                    }
                }

            } while (true);
        }   

        /*-------------------------------------------------------------------------
         * Function:    H5Z__scaleoffset_get_type
         *
         * Purpose:    Get the specific integer type based on datatype size and sign
         *              or floating-point type based on size
         *
         * Return:    Success: id number of integer type
         *        Failure: 0
         *
         * Programmer:    Xiaowen Wu
         *              Wednesday, April 13, 2005
         *
         *-------------------------------------------------------------------------
         */
        private static H5Z_scaleoffset H5Z__scaleoffset_get_type(uint dtype_class, uint dtype_size, uint dtype_sign)
        {
            var type = H5Z_scaleoffset.t_bad; /* integer type */

            if (dtype_class == H5Z_SCALEOFFSET_CLS_INTEGER) 
            {

                if (dtype_sign == H5Z_SCALEOFFSET_SGN_NONE) /* unsigned integer */
                { 
                    if (dtype_size == 8)
                        type = H5Z_scaleoffset.t_uchar;

                    else if (dtype_size == 16)
                        type = H5Z_scaleoffset.t_ushort;

                    else if (dtype_size == 32)
                        type = H5Z_scaleoffset.t_uint;

                    else if (dtype_size == 64)
                        type = H5Z_scaleoffset.t_ulong;

                    else
                        throw new Exception("Cannot find matched memory dataype.");
                }

                if (dtype_sign == H5Z_SCALEOFFSET_SGN_2) /* signed integer */
                { 
                    if (dtype_size == 8)
                        type = H5Z_scaleoffset.t_schar;

                    else if (dtype_size == 16)
                        type = H5Z_scaleoffset.t_short;

                    else if (dtype_size == 32)
                        type = H5Z_scaleoffset.t_int;

                    else if (dtype_size == 64)
                        type = H5Z_scaleoffset.t_long;

                    else
                        throw new Exception("Cannot find matched memory dataype.");
                }
            }

            if (dtype_class == H5Z_SCALEOFFSET_CLS_FLOAT)
            {
                if (dtype_size == sizeof(float))
                    type = H5Z_scaleoffset.t_float;

                else if (dtype_size == sizeof(double))
                    type = H5Z_scaleoffset.t_double;

                else
                    throw new Exception("Cannot find matched memory dataype.");
            }

            /* Set return value */
            return type;
        }

        #endregion
    }
}
