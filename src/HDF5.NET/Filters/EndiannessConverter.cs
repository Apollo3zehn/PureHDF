using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NET5_0
using System.Runtime.Intrinsics.X86;
#endif

namespace HDF5.NET
{
    public static class EndiannessConverter
    {
        public unsafe static void Convert<T>(Span<T> source, Span<T> destination) 
            where T : unmanaged
        {
            var bytesOfType = Unsafe.SizeOf<T>();
            EndiannessConverter.Convert(bytesOfType, MemoryMarshal.AsBytes(source), MemoryMarshal.AsBytes(destination));
        }

        public static unsafe void Convert(int bytesOfType, Span<byte> source, Span<byte> destination)
        {
#if NET5_0
            if (Avx2.IsSupported)
                EndiannessConverterAvx2.Convert(bytesOfType, source, destination);

            //else if (Sse2.IsSupported)
            //    EndiannessConverterSse2.Convert(bytesOfType, source, destination);

            else
#endif
                EndiannessConverterGeneric.Convert(bytesOfType, source, destination);
        }
    }
}