using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal partial record class DatatypeMessage
{
    /// <summary>
    /// Where a decoded element has to have its bytes reversed, as (offset, width) pairs relative to
    /// the start of one element.
    /// </summary>
    /// <remarks>
    /// The byte order belongs to each LEAF of a datatype, not to the datatype as a whole: a compound
    /// can hold one member of each order, and an enumeration carries its order one level down on its
    /// base type. A single answer per dataset therefore cannot be right, which is why this describes
    /// the element's layout instead.
    /// <para>
    /// Offsets are valid for the destination buffer as well as the file, because the only decoder
    /// that uses this copies file bytes verbatim - it runs when the memory type and the file type are
    /// the same size, so the two layouts are the same.
    /// </para>
    /// </remarks>
    internal readonly record struct ByteOrderSwap(int Offset, int Width);

    /// <summary>
    /// Collects the byte reversals this datatype needs, or reports that it needs one that cannot be
    /// expressed as a reversal of the decoded bytes.
    /// </summary>
    /// <returns>
    /// <see langword="false" /> if conversion is needed somewhere this pass cannot reach, in which
    /// case the caller must refuse the read rather than return unconverted data.
    /// </returns>
    internal bool TryCollectByteOrderSwaps(int baseOffset, List<ByteOrderSwap> swaps)
    {
        switch (Class)
        {
            case DatatypeMessageClass.FixedPoint:
            case DatatypeMessageClass.FloatingPoint:
            case DatatypeMessageClass.BitField:
                {
                    if (BitField is not IByteOrderAware byteOrderAware)
                        return true;

                    // VAX floating-point is a different representation, not a permutation of the
                    // little-endian one, so reversing the bytes would not produce the value.
                    if (byteOrderAware.ByteOrder == ByteOrder.VaxEndian)
                        return false;

                    var fileIsLittleEndian = byteOrderAware.ByteOrder == ByteOrder.LittleEndian;

                    if (fileIsLittleEndian != BitConverter.IsLittleEndian)
                        swaps.Add(new ByteOrderSwap(baseOffset, (int)Size));

                    return true;
                }

            case DatatypeMessageClass.Compound:
                foreach (CompoundPropertyDescription property in Properties)
                {
                    if (!property.MemberTypeMessage
                        .TryCollectByteOrderSwaps(baseOffset + (int)property.MemberByteOffset, swaps))
                        return false;
                }

                return true;

            case DatatypeMessageClass.Enumerated:
                return ((EnumerationPropertyDescription)Properties[0])
                    .BaseType
                    .TryCollectByteOrderSwaps(baseOffset, swaps);

            case DatatypeMessageClass.Array:
                {
                    var array = (ArrayPropertyDescription)Properties[0];
                    var baseSize = (int)array.BaseType.Size;
                    var count = array.DimensionSizes
                        .Aggregate(1UL, (product, dimension) => product * dimension);
                    var initialCount = swaps.Count;

                    if (!array.BaseType.TryCollectByteOrderSwaps(baseOffset, swaps))
                        return false;

                    var addedCount = swaps.Count - initialCount;

                    if (addedCount == 0)
                        return true;

                    var additionalCount = checked(addedCount * ((int)count - 1));
                    swaps.EnsureCapacity(swaps.Count + additionalCount);

                    // Unrolled rather than described once and repeated, because the swaps are applied
                    // as a flat list over the element and an array datatype is stored inline.
                    for (var i = 1UL; i < count; i++)
                    {
                        var elementOffset = (int)i * baseSize;

                        for (var j = 0; j < addedCount; j++)
                        {
                            var swap = swaps[initialCount + j];
                            swaps.Add(swap with { Offset = elementOffset + swap.Offset });
                        }
                    }

                    return true;
                }

            // A variable-length element holds a length and a heap ID inline, both of which are file
            // format structures with a fixed order of their own rather than the datatype's. The values
            // live on the global heap, which this pass does not see - so if the base type would need
            // converting, it cannot be done here and the read has to be refused instead.
            case DatatypeMessageClass.VariableLength:
                {
                    var baseType = ((VariableLengthPropertyDescription)Properties[0]).BaseType;
                    var nested = new List<ByteOrderSwap>();

                    return baseType.TryCollectByteOrderSwaps(0, nested) && nested.Count == 0;
                }

            // Bytes with no numeric interpretation, or addresses the format decoder reads itself.
            default:
                return true;
        }
    }

    /// <summary>
    /// Reverses every span the plan names, in every element of a decoded array.
    /// </summary>
    /// <remarks>
    /// Used by the decoders that bulk-copy a base type's bytes - an array datatype and a
    /// variable-length sequence - where the elements never pass through the per-leaf decoder that
    /// would otherwise have converted them.
    /// </remarks>
    internal static void ApplyByteOrderSwaps(Array array, int elementSize, ByteOrderSwap[] swaps)
    {
        if (swaps.Length == 0 || array.Length == 0)
            return;

        var bytes = MemoryMarshal.CreateSpan(
            ref MemoryMarshal.GetArrayDataReference(array),
            array.Length * elementSize
        );

        ApplyByteOrderSwaps(bytes, elementSize, swaps);
    }

    /// <summary>
    /// The plan for one value of this datatype, refusing what cannot be converted.
    /// </summary>
    internal ByteOrderSwap[] CollectByteOrderSwaps()
    {
        var swaps = new List<ByteOrderSwap>();

        if (!TryCollectByteOrderSwaps(baseOffset: 0, swaps))
            throw new Exception(
                "This data is stored in a byte order that PureHDF cannot convert: either VAX "
                + "floating-point, or a variable-length datatype whose values need conversion.");

        return [.. swaps];
    }

    /// <summary>Reverses one value's bytes in place.</summary>
    internal static T ReverseBytes<T>(T value) where T : struct
    {
        var span = MemoryMarshal.CreateSpan(ref value, 1);
        MemoryMarshal.AsBytes(span).Reverse();

        return value;
    }

    /// <summary>
    /// Reverses every span the plan names, in every element of <paramref name="bytes" />.
    /// </summary>
    internal static void ApplyByteOrderSwaps(Span<byte> bytes, int elementSize, ByteOrderSwap[] swaps)
    {
        for (var start = 0; start + elementSize <= bytes.Length; start += elementSize)
        {
            foreach (var swap in swaps)
            {
                bytes.Slice(start + swap.Offset, swap.Width).Reverse();
            }
        }
    }
}
