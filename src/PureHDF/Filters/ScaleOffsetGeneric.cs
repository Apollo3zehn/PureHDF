using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF
{
    // https://portal.hdfgroup.org/display/HDF5/HDF5+User's+Guide: 5.6.2. Using the Scale - offset Filter
    // https://forum.hdfgroup.org/t/scale-offset-filter-and-special-float-values-nan-infinity/3379
    internal static class ScaleOffsetGeneric
    {
        #region Decompression

        public static Memory<byte> Decompress(
            Memory<byte> data, uint[] parametersBuffer)
        {
            // H5Zscaleoffset.c (H5Z__filter_scaleoffset)

            const int payloadOffset = 21;

            var parameters = ScaleOffsetGeneric.ValidateAndPrepareParameters(parametersBuffer);
            var spanData = data.Span;

            /* minbits */
            uint minbits = 0;

            for (int i = 0; i < 4; i++)
            {
                uint minbits_mask = spanData[i];
                minbits_mask <<= i * 8;
                minbits |= minbits_mask;
            }

            /* minval */
            var minval_size = spanData[4];
            var minval = new byte[minval_size];

            for (int i = 0; i < minval_size; i++)
            {
                minval[i] = spanData[5 + i];
            }

            if (minbits > parameters.Size * 8)
                throw new Exception("minbits > p.size * 8");

            /* allocate memory space for decompressed buffer */
            int defilteredBufferSize = (int)(parameters.ElementCount * parameters.Size);
            var output = new byte[defilteredBufferSize];

            /* special case: minbits equal to full precision */
            if (minbits == parameters.Size * 8)
            {
                spanData
[payloadOffset..]
                    .CopyTo(output);
            }

            /* normal case */
            else
            {
                /* decompress the buffer if minbits not equal to zero */
                if (minbits != 0)
                    ScaleOffsetGeneric.DecompressAll(output, spanData[payloadOffset..], parameters, minbits);

                /* postprocess after decompression */
                if (parameters.Class == Class.Integer)
                {
                    if (parameters.Sign == Sign.Unsigned)
                    {
                        if (parameters.Size == 1)
                            PostdecompressByte(MemoryMarshal.Cast<byte, byte>(output), parameters, minbits, minval);

                        else if (parameters.Size == 2)
                            PostdecompressInteger(MemoryMarshal.Cast<byte, ushort>(output), parameters, minbits, minval);

                        else if (parameters.Size == 4)
                            PostdecompressInteger(MemoryMarshal.Cast<byte, uint>(output), parameters, minbits, minval);

                        else if (parameters.Size == 8)
                            PostdecompressInteger(MemoryMarshal.Cast<byte, ulong>(output), parameters, minbits, minval);

                        else
                            throw new Exception("Unsupported data type.");
                    }

                    else
                    {
                        if (parameters.Size == 1)
                            PostdecompressSByte(MemoryMarshal.Cast<byte, sbyte>(output), parameters, minbits, minval);

                        else if (parameters.Size == 2)
                            PostdecompressInteger(MemoryMarshal.Cast<byte, short>(output), parameters, minbits, minval);

                        else if (parameters.Size == 4)
                            PostdecompressInteger(MemoryMarshal.Cast<byte, int>(output), parameters, minbits, minval);

                        else if (parameters.Size == 8)
                            PostdecompressInteger(MemoryMarshal.Cast<byte, long>(output), parameters, minbits, minval);

                        else
                            throw new Exception("Unsupported data type.");
                    }
                }

                else if (parameters.Class == Class.Float)
                {
                    if (parameters.ScaleType == ScaleType.FLOAT_DSCALE)
                    {
                        if (parameters.Size == 4)
                            PostdecompressFloat32(MemoryMarshal.Cast<byte, float>(output), parameters, minbits, minval);

                        else if (parameters.Size == 8)
                            PostdecompressFloat64(MemoryMarshal.Cast<byte, double>(output), parameters, minbits, minval);

                        else
                            throw new Exception("Unsupported data type.");
                    }
                }

                else
                    throw new Exception("Unsupported data type.");
            }

            /* after postprocess, convert to dataset datatype endianness order if needed */
            //if (need_convert)
            //    ScaleoOffsetGeneric.Convert(output, parameters);

            return output;
        }

        private static void DecompressAll(
            Span<byte> output, Span<byte> input, Parameters parameters, uint minbits)
        {
            /* j: index of buffer,
               buf_len: number of bits to be filled in current byte */

            /* initialization before the loop */
            int j = 0;
            uint buf_len = sizeof(byte) * 8;

            /* decompress */
            for (int i = 0; i < parameters.ElementCount; i++)
            {
                ScaleOffsetGeneric.DecompressOneAtomic(
                    output[(i * (int)parameters.Size)..],
                    input,
                    ref j,
                    ref buf_len,
                    parameters.Size,
                    minbits);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecompressOneAtomic(
            Span<byte> data,
            Span<byte> buffer,
            ref int j,
            ref uint buf_len,
            uint size,
            uint minbits)
        {
            /* begin_i: the index of byte having first significant bit */
            uint begin_i;

            if (minbits <= 0)
                throw new Exception("p.minbits <= 0");

            uint dtype_len = size * 8;

            if (BitConverter.IsLittleEndian)
            {
                begin_i = size - 1 - (dtype_len - minbits) / 8;

                for (int k = (int)begin_i; k >= 0; k--)
                    DecompressOneByte(data, k, begin_i, buffer, ref j, ref buf_len, dtype_len, minbits);
            }
            else
            {
                begin_i = (dtype_len - minbits) / 8;

                for (int k = (int)begin_i; k <= (int)(size - 1); k++)
                    DecompressOneByte(data, k, begin_i, buffer, ref j, ref buf_len, dtype_len, minbits);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecompressOneByte(
            Span<byte> data,
            int k,
            uint begin_i,
            Span<byte> buffer,
            ref int j,
            ref uint buf_len,
            uint dtype_len,
            uint minbits)
        {
            uint dat_len; /* dat_len is the number of bits to be copied in each data byte */
            byte val;     /* value to be copied in each data byte */

            /* initialize value and bits of unsigned char to be copied */
            val = buffer[j];

            if (k == begin_i)
                dat_len = 8 - (dtype_len - minbits) % 8;

            else
                dat_len = 8;

            if (buf_len > dat_len)
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

        #endregion

        #region Postdecompression

        private static unsafe void PostdecompressByte(
            Span<byte> output, Parameters parameters, uint minbits, byte[] minvalRaw)
        {
            byte minval = minvalRaw[0];
            byte maxval = BitConverter.GetBytes((1UL << (int)minbits) - 1UL)[0];

            if (parameters.FillValueDefined == FillValueDefined.Defined)
            {
                byte filval = unchecked((byte)parameters.FillValue[0]);

                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = output[i].Equals(maxval)
                        ? filval
                        : (byte)(output[i] + minval);
                }
            }

            else
            {
                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = (byte)(output[i] + minval);
                }
            }
        }

        private static unsafe void PostdecompressSByte(
            Span<sbyte> output, Parameters parameters, uint minbits, byte[] minvalRaw)
        {
            sbyte minval = (sbyte)minvalRaw[0];
            sbyte maxval = (sbyte)BitConverter.GetBytes((1UL << (int)minbits) - 1UL)[0];

            if (parameters.FillValueDefined == FillValueDefined.Defined)
            {
                sbyte filval = unchecked((sbyte)parameters.FillValue[0]);

                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = output[i].Equals(maxval)
                        ? filval
                        : (sbyte)(output[i] + minval);
                }
            }

            else
            {
                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = (sbyte)(output[i] + minval);
                }
            }
        }

        private static unsafe void PostdecompressInteger<T>(
            Span<T> output, Parameters parameters, uint minbits, byte[] minvalRaw)
            where T : struct
        {
            T minval = MemoryMarshal.Cast<byte, T>(minvalRaw)[0];
            T maxval = MemoryMarshal.Cast<byte, T>(BitConverter.GetBytes((1UL << (int)minbits) - 1UL))[0];

            if (parameters.FillValueDefined == FillValueDefined.Defined)
            {
                T filval = GetFillValue<T>(new Span<uint>(parameters.FillValue, 12));

                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = output[i].Equals(maxval)
                        ? filval
                        : GenericAdd<T>.Add(output[i], minval);
                }
            }

            else
            {
                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = GenericAdd<T>.Add(output[i], minval);
                }
            }
        }

        private static unsafe void PostdecompressFloat32(
            Span<float> output, Parameters parameters, uint minbits, byte[] minvalRaw)
        {
            var minval = MemoryMarshal.Cast<byte, float>(minvalRaw)[0];
            var maxval = MemoryMarshal.Cast<byte, float>(BitConverter.GetBytes((1UL << (int)minbits) - 1UL))[0];
            var scaleFactor = (float)Math.Pow(10, parameters.ScaleFactor);

            Span<float> buffer = stackalloc float[1];
            var bufferAsInt = MemoryMarshal.Cast<float, int>(buffer);

            if (parameters.FillValueDefined == FillValueDefined.Defined)
            {
                var filval = GetFillValue<float>(new Span<uint>(parameters.FillValue, 12));

                for (int i = 0; i < output.Length; i++)
                {
                    buffer[0] = output[i];

                    output[i] = output[i].Equals(maxval)
                        ? filval
                        : (float)(bufferAsInt[0] / scaleFactor + minval);
                }
            }

            else
            {
                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = (float)(bufferAsInt[0] / scaleFactor + minval);
                }
            }
        }

        private static unsafe void PostdecompressFloat64(
            Span<double> output, Parameters parameters, uint minbits, byte[] minvalRaw)
        {
            var minval = MemoryMarshal.Cast<byte, double>(minvalRaw)[0];
            var maxval = MemoryMarshal.Cast<byte, double>(BitConverter.GetBytes((1UL << (int)minbits) - 1UL))[0];
            var scaleFactor = (double)Math.Pow(10, parameters.ScaleFactor);

            Span<double> buffer = stackalloc double[1];
            var bufferAsLong = MemoryMarshal.Cast<double, long>(buffer);

            if (parameters.FillValueDefined == FillValueDefined.Defined)
            {
                var filval = GetFillValue<double>(new Span<uint>(parameters.FillValue, 12));

                for (int i = 0; i < output.Length; i++)
                {
                    buffer[0] = output[i];

                    output[i] = output[i].Equals(maxval)
                        ? filval
                        : (double)(bufferAsLong[0] / scaleFactor + minval);
                }
            }

            else
            {
                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = (double)(bufferAsLong[0] / scaleFactor + minval);
                }
            }
        }

        #endregion

        #region Helpers

        private static Parameters ValidateAndPrepareParameters(
            uint[] parametersBuffer)
        {
            // H5Zscaleoffset.c (H5Z__filter_scaleoffset)

            /* check arguments */
            if (parametersBuffer.Length != 20)
                throw new Exception("Invalid scaleoffset number of parameters.");

            var parameters = MemoryMarshal.Cast<uint, Parameters>(parametersBuffer)[0];

            /* Check if memory byte order matches dataset datatype byte order */
            bool need_convert = false;

            if (BitConverter.IsLittleEndian)
            {
                if (parameters.ByteOrder == ByteOrder.BigEndian)
                    need_convert = true;
            }
            else
            {
                if (parameters.ByteOrder == ByteOrder.LittleEndian)
                    need_convert = true;
            }

            // TODO: This is not supported because after filtering, the data are native endianess and then HDF5 lib converts them later again.
            if (need_convert)
                throw new NotSupportedException("Scale-offset data conversion from big endian to little endian or vice versa is not supported.");

            if (parameters.Class == Class.Float)
            {
                if (parameters.ScaleType != ScaleType.FLOAT_DSCALE && parameters.ScaleType != ScaleType.FLOAT_ESCALE)
                    throw new Exception("Invalid scale type.");
            }

            else if (parameters.Class == Class.Integer)
            {
                if (parameters.ScaleType != ScaleType.INT)
                    throw new Exception("Invalid scale type.");
            }

            return parameters;
        }

        private static T GetFillValue<T>(
            Span<uint> parameters)
             where T : struct
        {
            var i = 0;
            var copySize = sizeof(uint);
            var fillValue = new byte[Marshal.SizeOf<T>()];
            var slicedFillValue = fillValue.AsSpan();

            if (BitConverter.IsLittleEndian)
            {
                Span<uint> _cd_value = stackalloc uint[1];

                /* Copy 4 bytes at a time to each cd value */
                do
                {
                    if (slicedFillValue.Length < 4)
                        copySize = slicedFillValue.Length;

                    /* Copy the value */
                    _cd_value[0] = parameters[i];

                    MemoryMarshal
                        .AsBytes(_cd_value)
[..copySize]
                        .CopyTo(slicedFillValue);

                    /* Next field */
                    i++;
                    slicedFillValue = slicedFillValue[copySize..];

                } while (!slicedFillValue.IsEmpty);
            }
            else
            {
                throw new NotSupportedException("The scale-offset decompression is not supported on big-endian systems.");
            }

            return MemoryMarshal.Cast<byte, T>(fillValue)[0];
        }

        #endregion

        #region Types

        internal static class GenericAdd<T>
        {
            private static readonly Func<T, T, T> _add_function = GenericAdd<T>.EmitAddFunction();

            private static Func<T, T, T> EmitAddFunction()
            {
                var _parameterA = Expression.Parameter(typeof(T), "a");
                var _parameterB = Expression.Parameter(typeof(T), "b");

                var _body = Expression.Add(_parameterA, _parameterB);

                return Expression.Lambda<Func<T, T, T>>(_body, _parameterA, _parameterB).Compile();
            }

            public static T Add(T a, T b)
            {
                return _add_function(a, b);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private unsafe struct Parameters
        {
            public ScaleType ScaleType;
            public uint ScaleFactor;
            public uint ElementCount;
            public Class Class;
            public uint Size;
            public Sign Sign;
            public ByteOrder ByteOrder;
            public FillValueDefined FillValueDefined;
            public fixed uint FillValue[12];
        }

        private enum ScaleType : uint
        {
            FLOAT_DSCALE = 0,
            FLOAT_ESCALE = 1,
            INT = 2
        }

        private enum ByteOrder : uint
        {
            LittleEndian = 0,
            BigEndian = 1,
        }

        private enum Class : uint
        {
            Integer = 0,
            Float = 1,
        }

        private enum Sign : uint
        {
            Unsigned = 0,
            TwosComplement = 1,
        }

        private enum FillValueDefined : uint
        {
            Undefined = 0,
            Defined = 1,
        }

        #endregion
    }
}
