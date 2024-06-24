# Simple Data

Each HDF5 attribute or dataset has an associated dataspace which is either of type `null`, `scalar` or `simple`. The following overview shows you how to handle these different dataspace types:

- `null`: You cannot read `null` dataspaces as they contain no data.
- `scalar`: Provide a compatible scalar return type. To read the value of a scalar dataset of type `int` call `dataset.Read<int>()`.
- `simple`: Provide a compatible array return type. To read the value of a 2D dataset of type `int` call `dataset.Read<int[,]>()`

The following section shows you how to read 1D datasets of any datatype. All samples shown work for datasets as well as attributes.

```cs
// class: fixed-point

    var data = dataset.Read<int[]>();

// class: floating-point

    var data = dataset.Read<double[]>();

// class: string

    var data = dataset.Read<string[]>();

// class: bitfield

    [Flags]
    enum SystemStatus : ushort /* make sure the enum in HDF file is based on the same type */
    {
        MainValve_Open          = 0x0001,
        AuxValve_1_Open         = 0x0002,
        AuxValve_2_Open         = 0x0004,
        MainEngine_Ready        = 0x0008,
        FallbackEngine_Ready    = 0x0010
        // ...
    }

    var data = dataset.Read<SystemStatus[]>();
    var readyToLaunch = data[0].HasFlag(SystemStatus.MainValve_Open | SystemStatus.MainEngine_Ready);

// class: opaque

    var data = dataset.Read<MyOpaqueStruct[]>();

// class: compound

    /* option 1: faster (if no reference types are contained) */
    var data = dataset.Read<MyNonNullableStruct[]>();

    /* option 2: slower (useful to read unknown structs) */
    var data = dataset.Read<Dictionary<string, object>>();

// class: reference @ object reference

    var references = dataset.Read<NativeObjectReference1[]>();
    var firstRef = references.First();

    /* NOTE: Dereferencing would be quite fast if the object's name
     * was known. Instead, the library searches recursively for the  
     * object. Do not dereference using a parent (group) that contains
     * any circular soft links. Hard links are no problem.
     */

    /* option 1 (faster) */
    var firstObject = directParent.Get(firstRef);

    /* option 1 (slower, use if you don't know the objects parent) */
    var firstObject = root.Get(firstRef);

// class: reference @ region reference

    var references = dataset.Read<NativeRegionReference1[]>();
    var firstRef = references.First();
    var selection = root.Get(firstRef);
    var data = referencedDataset.Read<T>(fileSelection: selection);

// class: enumerated

    enum MyEnum : short /* make sure the enum in HDF file is based on the same type */
    {
        MyValue1 = 1,
        MyValue2 = 2,
        // ...
    }

    var data = dataset.Read<MyEnum[]>();

// class: variable length strings

    var data = dataset.Read<string[]>();

// class: variable length sequences

    var data = dataset.Read<T[][]>();

// class: array

    var data = dataset.Read<T[][]>;

// class: time
// -> not supported (reason: the HDF5 C lib itself does not fully support H5T_TIME)
```
> [!NOTE]
> For more information about compound data, see section [Compound Data](compound.md).