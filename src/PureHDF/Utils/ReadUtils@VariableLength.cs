namespace PureHDF;

internal static partial class ReadUtils
{
    public static Memory<T[]> ReadVariableLengthSequence<T>(
        NativeReadContext context, 
        DatatypeMessage datatype, 
        Span<byte> source,
        Memory<T[]> destination) where T : struct
    {
        // https://github.com/HDFGroup/hdf5/blob/1d90890a7b38834074169ce56720b7ea7f4b01ae/src/H5Tpublic.h#L1621-L1642
        // https://portal.hdfgroup.org/display/HDF5/Datatype+Basics#DatatypeBasics-variable
        // https://github.com/HDFGroup/hdf5/blob/1d90890a7b38834074169ce56720b7ea7f4b01ae/test/tarray.c#L1113
        // https://github.com/HDFGroup/hdf5/blob/1d90890a7b38834074169ce56720b7ea7f4b01ae/src/H5Tpublic.h#L234-L241
        // https://github.com/HDFGroup/hdf5/blob/1d90890a7b38834074169ce56720b7ea7f4b01ae/src/H5Tvlen.c#L837-L941
        //
        // typedef struct {
        //     size_t len; /**< Length of VL data (in base type units) */
        //     void  *p;   /**< Pointer to VL data */
        // } hvl_t;

        if (datatype.Class != DatatypeMessageClass.VariableLength)
            throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.VariableLength}'.");

        var properties = (VariableLengthPropertyDescription)datatype.Properties[0];
        var destinationSpan = destination.Span;

        using var localDriver = new H5StreamDriver(new MemoryStream(source.ToArray()), leaveOpen: false);

        var genericMethodInfo = DataUtils.MethodInfoCastToArray.MakeGenericMethod(typeof(T));
        var parameters = new object[1];
        var isReferenceOrContainsReferences = DataUtils.IsReferenceOrContainsReferences(typeof(T));

        for (int i = 0; i < destinationSpan.Length; i++)
        {
            var length = localDriver.ReadUInt32();
            var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, localDriver);

            if (globalHeapId.Equals(default))
            {
                destinationSpan[i] = default!;
                continue;
            }

            var globalHeapCollection = NativeCache.GetGlobalHeapObject(context, globalHeapId.CollectionAddress);

            if (globalHeapCollection.GlobalHeapObjects.TryGetValue((int)globalHeapId.ObjectIndex, out var globalHeapObject))
            {
                if (isReferenceOrContainsReferences)
                {
                    var result = new T[length];

                    ReadCompound<T>(
                        context, 
                        properties.BaseType, 
                        globalHeapObject.ObjectData, 
                        result, 
                        fieldInfo => fieldInfo.Name);

                    destinationSpan[i] = result;
                }

                else
                {
                    parameters[0] = globalHeapObject.ObjectData;
                    destinationSpan[i] = (T[])genericMethodInfo.Invoke(default, parameters)!;
                }
            }
            
            else
            {
                // It would be more correct to just throw an exception 
                // when the object index is not found in the collection,
                // but that would make the tests following tests fail
                // - CanReadDataset_Array_nullable_struct
                // - CanReadDataset_Array_nullable_struct.
                // 
                // And it would make the user's life a bit more complicated
                // if the library cannot handle missing entries.
                // 
                // Since this behavior is not according to the spec, this
                // method still returns `T[]` instead of nullable 
                // `T[]?`.
                destinationSpan[i] = default!;
            }
        }

        return destination;
    }
}