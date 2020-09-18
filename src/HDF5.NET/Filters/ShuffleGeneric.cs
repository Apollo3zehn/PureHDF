using System;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    public static class ShuffleGeneric
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Shuffle(ulong type_size, ulong vectorizable_blocksize, ulong blocksize, byte* source, byte* dest)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Unshuffle(ulong type_size, ulong vectorizable_blocksize, ulong blocksize, byte* source, byte* dest)
        {
            throw new NotImplementedException();
        }
    }
}
