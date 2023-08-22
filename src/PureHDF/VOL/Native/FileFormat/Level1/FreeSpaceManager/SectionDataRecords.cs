namespace PureHDF.VOL.Native;

// internal abstract record class SectionDataRecord
// (
//     //
// );

// internal record class FilesSectionDataRecord() : SectionDataRecord
// {
//     //
// }

// internal record class FractalHeapFirstRowSectionDataRecord(
//     //
// ) : SectionDataRecord;

// internal record class FractalHeapNormalRowSectionDataRecord(
//     //
// ) : SectionDataRecord;

// internal record class FractalHeapIndirectSectionDataRecord(
//     ulong FractalHeapIndirectBlockOffset,
//     ushort BlockStartRow,
//     ushort BlockStartColumn,
//     ushort BlockCount
// ) : SectionDataRecord
// {
//     public static FractalHeapIndirectSectionDataRecord Decode(H5DriverBase driver)
//     {
//         // TODO: implement this correctly
//         // FractalHeapIndirectBlockOffset = driver.ReadBytes(8);

//         return new FractalHeapIndirectSectionDataRecord(
//             FractalHeapIndirectBlockOffset: default,
//             BlockStartRow: driver.ReadUInt16(),
//             BlockStartColumn: driver.ReadUInt16(),
//             BlockCount: driver.ReadUInt16()
//         );
//     }
// }

// internal record class FractalHeapSingleSectionDataRecord(
//     ulong FractalHeapIndirectBlockOffset,
//     ushort BlockStartRow,
//     ushort BlockStartColumn,
//     ushort BlockCount
// ) : FractalHeapIndirectSectionDataRecord(
//     FractalHeapIndirectBlockOffset,
//     BlockStartRow,
//     BlockStartColumn,
//     BlockCount)
// {
//     public static FractalHeapSingleSectionDataRecord Decode(H5DriverBase driver)
//     {
//         // TODO: implement this correctly
//         // FractalHeapIndirectBlockOffset = driver.ReadBytes(8);

//         return new FractalHeapSingleSectionDataRecord(
//             FractalHeapIndirectBlockOffset: default,
//             BlockStartRow: driver.ReadUInt16(),
//             BlockStartColumn: driver.ReadUInt16(),
//             BlockCount: driver.ReadUInt16()
//         );
//     }
// }