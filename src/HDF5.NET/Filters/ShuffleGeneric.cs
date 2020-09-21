using System;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    public static class ShuffleGeneric
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Shuffle(int type_size, int vectorizable_blocksize, int blocksize, byte* source, byte* dest)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Unshuffle(int type_size, int vectorizable_blocksize, int blocksize, byte* source, byte* dest)
        {
            throw new NotImplementedException();
        }
    }
}
