# PureHDF

[![NuGet](https://img.shields.io/nuget/vpre/PureHDF.svg?label=Nuget)](https://www.nuget.org/packages/PureHDF)

A pure C# library without native dependencies that makes reading and writing of HDF5 files (groups, datasets, attributes, ...) very easy.

The minimum supported target framework is .NET Standard 2.0 which includes
- .NET Framework 4.6.1+ 
- .NET Core (all versions)
- .NET 5+

This library runs on all platforms (ARM, x86, x64) and operating systems (Linux, Windows, MacOS, Raspbian, etc) that are supported by the .NET ecosystem without special configuration.

The implemention follows the [HDF5 File Format Specification (HDF5 1.10)](https://docs.hdfgroup.org/hdf5/v1_10/_f_m_t3.html).

> Please read the [reading](reading/index.md) or [writing](writing/index.md) docs to get started with PureHDF.

# Installation

```bash
dotnet add package PureHDF --prerelease
```

# Feature overview

| Reading | Writing | Feature                      |
| ------- | ------- | ---------------------------- |
| &check; | &check; | generic API                  |
| &check; | &check; | easy filter access           |
| &check; | &check; | hardware-accelerated filters |
| &check; | &check; | data slicing                 |
| &check; | &check; | multidimensional arrays      |
| &check; | &check; | compound data                |
| &check; | &check; | variable-length data         |
| &check; | -       | multithreading (^1)          |
| &check; | -       | Amazon S3 access             |
| &check; | -       | HSDS (^2) access             |

# Comparison table
Overwhelmed by the number of different HDF 5 libraries? Here is a comparison table:

> Note: The following table considers only projects listed on Nuget.org

|         Name                                                                      | Arch    | Platform    | Kind     | Mode | Version   | License     | Maintainer         | Comment              |  
| --------------------------------------------------------------------------------- | ------- | ----------- | -------- | ---- | --------- | ----------- | ------------------ | -------------------- |  
| **v1.10**                                                                         |         |             |          |      |           |             |                    |                      |  
| [PureHDF](https://www.nuget.org/packages/PureHDF)                                 | all     | all         | managed  | rw   | 1.10.*    | MIT         | Apollo3zehn        |                      |
| [HDF5-CSharp](https://www.nuget.org/packages/HDF5-CSharp)                         | x86,x64 | Win,Lin,Mac | HL       | rw   | 1.10.6    | MIT         | LiorBanai          |                      |  
| [SciSharp.Keras.HDF5](https://www.nuget.org/packages/SciSharp.Keras.HDF5)         | x86,x64 | Win,Lin,Mac | HL       | rw   | 1.10.5    | MIT         | SciSharp           | fork of HDF-CSharp   |  
| [ILNumerics.IO.HDF5](https://www.nuget.org/packages/ILNumerics.IO.HDF5)           | x64     | Win,Lin     | HL       | rw   | ?         | proprietary | IL\_Numerics\_GmbH | probably 1.10        |  
| [LiteHDF](https://www.nuget.org/packages/LiteHDF)                                 | x86,x64 | Win,Lin,Mac | HL       | ro   | 1.10.5    | MIT         | silkfire           |                      |  
| [hdflib](https://www.nuget.org/packages/hdflib)                                   | x86,x64 | Windows     | HL       | wo   | 1.10.6    | MIT         | bdebree            |                      |  
| [Mbc.Hdf5Utils](https://www.nuget.org/packages/Mbc.Hdf5Utils)                     | x86,x64 | Win,Lin,Mac | HL       | rw   | 1.10.6    | Apache-2.0  | bqstony            |                      |  
| [HDF.PInvoke](https://www.nuget.org/packages/HDF.PInvoke)                         | x86,x64 | Windows     | bindings | rw   | 1.8,1.10.6| HDF5        | hdf,gheber         |                      |  
| [HDF.PInvoke.1.10](https://www.nuget.org/packages/HDF.PInvoke.1.10)               | x86,x64 | Win,Lin,Mac | bindings | rw   | 1.10.6    | HDF5        | hdf,Apollo3zehn    |                      |  
| [HDF.PInvoke.NETStandard](https://www.nuget.org/packages/HDF.PInvoke.NETStandard) | x86,x64 | Win,Lin,Mac | bindings | rw   | 1.10.5    | HDF5        | surban             |                      |  
| **v1.8**                                                                          |         |             |          |      |           |             |                    |                      |  
| [HDF5DotNet.x64](https://www.nuget.org/packages/HDF5DotNet.x64)                   | x64     | Windows     | HL       | rw   | 1.8       | HDF5        | thieum             |                      |  
| [HDF5DotNet.x86](https://www.nuget.org/packages/HDF5DotNet.x86)                   | x86     | Windows     | HL       | rw   | 1.8       | HDF5        | thieum             |                      |  
| [sharpHDF](https://www.nuget.org/packages/sharpHDF)                               | x64     | Windows     | HL       | rw   | 1.8       | MIT         | bengecko           |                      |  
| [HDF.PInvoke](https://www.nuget.org/packages/HDF.PInvoke)                         | x86,x64 | Windows     | bindings | rw   | 1.8,1.10.6| HDF5        | hdf,gheber         |                      |  
| [hdf5-v120-complete](https://www.nuget.org/packages/hdf5-v120-complete)           | x86,x64 | Windows     | native   | rw   | 1.8       | HDF5        | daniel.gracia      |                      |  
| [hdf5-v120](https://www.nuget.org/packages/hdf5-v120)                             | x86,x64 | Windows     | native   | rw   | 1.8       | HDF5        | keen               |                      |  

**Abbreviations:**

| Term      | .NET API   | Native dependencies |
| --------- | ---------- | ------------------- |
| `managed` | high-level | none                |
| `HL`      | high-level | C-library           |
| `bindings`| low-level  | C-library           |
| `native`  | none       | C-library           |