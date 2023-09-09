# PureHDF

[![GitHub Actions](https://github.com/Apollo3zehn/PureHDF/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/Apollo3zehn/PureHDF/actions) [![NuGet](https://img.shields.io/nuget/vpre/PureHDF.svg?label=Nuget)](https://www.nuget.org/packages/PureHDF)

A pure C# library without native dependencies that makes reading and writing of HDF5 files (groups, datasets, attributes, ...) very easy.

The minimum supported target framework is .NET Standard 2.0 which includes
- .NET Framework 4.6.1+ 
- .NET Core (all versions)
- .NET 5+

This library runs on all platforms (ARM, x86, x64) and operating systems (Linux, Windows, MacOS, Raspbian, etc) that are supported by the .NET ecosystem without special configuration.

The implemention follows the [HDF5 File Format Specification (HDF5 1.10)](https://docs.hdfgroup.org/hdf5/v1_10/_f_m_t3.html).

> Please read the [docs](https://apollo3zehn.github.io/PureHDF/) for samples and API documentation.

# Installation

```bash
dotnet add package PureHDF --prerelease
```

# Quick Start

## Reading

```cs
// root group
var file = H5File.OpenRead("path/to/file.h5");

// sub group
var group = file.Group("path/to/group");

// attribute
var attribute = group.Attribute("my-attribute");
var attributeData = attribute.Read<int>();

// dataset
var dataset = group.Dataset("my-dataset");
var datasetData = dataset.Read<double>();
```

See the [docs](https://apollo3zehn.github.io/PureHDF/reading/index.html) to learn more about `data types`, `multidimensional arrays`, `chunks`, `compression`, `slicing` and more.

## Writing

The first step is to create a new `H5File` instance:

```cs
var file = new H5File();
```

A `H5File` derives from the `H5Group` type because it represents the root group. `H5Group` implements the `IDictionary` interface, where the keys represent the links in an HDF5 file and the value determines the type of the link: either it is another `H5Group` or a `H5Dataset`. 

You can create an empty group like this:

```cs
var group = new H5Group();
```

If the group should have some datasets, just add them using the dictionary collection initializer - just like with a normal dictionary:

```cs
var group = new H5Group()
{
    ["numerical-dataset"] = new double[] { 2.0, 3.1, 4.2 },
    ["string-dataset"] = new string[] { "One", "Two", "Three" }
}
```

Datasets and attributes can both be created either by instantiating their specific class (`H5Dataset`, `H5Attribute`) or by just providing some kind of data. This data can be nearly anything: arrays, scalars, numerical values, strings, anonymous types, enums, complex objects, structs, bool values, etc. However, whenever you want to provide more details like the dimensionality of the attribute or dataset, the chunk layout or the filters to be applied to a dataset, you need to instantiate the appropriate class.

But first, let's see how to add attributes. Attributes cannot be added directly using the dictionary collection initializer because that is only for datasets. However, every `H5Group` has an `Attribute` property which accepts our attributes:

```cs
var group = new H5Group()
{
    Attributes = new()
    {
        ["numerical-attribute"] = new double[] { 2.0, 3.1, 4.2 },
        ["string-attribute"] = new string[] { "One", "Two", "Three" }
    }
}
```

The full example with the root group, a subgroup, two datasets and two attributes looks like this:

```cs
var file = new H5File()
{
    ["my-group"] = new H5Group()
    {
        ["numerical-dataset"] = new double[] { 2.0, 3.1, 4.2 },
        ["string-dataset"] = new string[] { "One", "Two", "Three" },
        Attributes = new()
        {
            ["numerical-attribute"] = new double[] { 2.0, 3.1, 4.2 },
            ["string-attribute"] = new string[] { "One", "Two", "Three" }
        }
    }
};
```

The last step is to write the defined file to the drive:

```cs
file.Write("path/to/file.h5");
```

See the [docs](https://apollo3zehn.github.io/PureHDF/writing/index.html) to learn more about `data types`, `multidimensional arrays`, `chunks`, `compression`, `slicing` and more.

# Development

The tests of PureHDF are executed against `.NET 6` and `.NET 7` so these two runtimes are required. Please note that due to an currently unknown reason the writing tests cannot be run in parallel to other tests because some unrelated temp files are in use although they should not be and thus cannot be accessed by the unit tests.

If you are using Visual Studio Code as your IDE, you can simply execute one of the predefined test tasks by selecting `Run Tasks` from the global menu (`Ctrl+Shift+P`). The following test tasks are predefined:

- `tests: common`
- `tests: writing`
- `tests: filters`
- `tests: HSDS`

The HSDS tests require a python installation to be present on the system with the `venv` package available.

# Comparison Table
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