// Disabled until the new reference types will be implemented

// using System.Runtime.InteropServices;

// namespace PureHDF;

// /// <summary>
// /// An HDF5 attribute reference.
// /// </summary>
// [StructLayout(LayoutKind.Explicit, Size = 8)]
// public partial struct NativeAttributeReference
// {
//     [FieldOffset(0)]
//     internal NativeReferenceHeader Header;

//     [FieldOffset(2)]
//     internal byte TokenSize;
// }

// internal enum NativeReferenceType : byte
// {
//     Object_2 = 2,

//     DatasetRegion_2 = 3,

//     Attribute = 4
// }

// [StructLayout(LayoutKind.Explicit, Size = 2)]
// internal partial struct NativeReferenceHeader
// {
//     [FieldOffset(0)]
//     internal NativeReferenceType Type;

//     [FieldOffset(1)]
//     internal byte Flags;
// }