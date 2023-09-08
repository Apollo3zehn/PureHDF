# Intellisense

| Requires  |
| --------- |
| `.NET 5+` |

## Introduction

Consider the following H5 file:

![HDF View](https://github.com/Apollo3zehn/PureHDF/raw/master/doc/images/hdfview.png)

If you would like to access `sub_dataset2` you would normally do

```cs
    using var h5File = H5File.OpenRead(FILE_PATH);
    var dataset = h5File.Group("group1").Dataset("sub_dataset2");
```

When you have files with a large number of groups or a deep hierarchy and you often need to work on different paths within the file, it could very useful to get intellisense support from your favourite IDE which helps you navigating through the file.

PureHDF utilizes the source generator feature introduced with .NET 5 which allows to generate additional code during compilation. The generator, which comes with the `PureHDF.SourceGenerator` package, enables you to interact with the H5 file like this:

```cs
var dataset = bindings.group1.sub_dataset2;
```

## Getting Started

Run the following command:

```bash
dotnet add package PureHDF.SourceGenerator
dotnet restore
```

> [!NOTE]
> Make sure that all project dependencies are restored before you continue.

Then define the path to your H5 file from which the bindings should be generated and use it in combination with the `H5SourceGenerator` attribute:

```cs
using PureHDF;

[H5SourceGenerator(filePath: Program.FILE_PATH)]
internal partial class MyGeneratedH5Bindings {};

static class Program
{
    public const string FILE_PATH = "myFile.h5";

    static void Main()
    {
        using var h5File = H5File.OpenRead(FILE_PATH);
        var bindings = new MyGeneratedH5Bindings(h5File);
        var myDataset = bindings.group1.sub_dataset2;
    }
}
```

Your IDE should now run the source generator behind the scenes and you should be able to get intellisense support:

![Intellisense](https://github.com/Apollo3zehn/PureHDF/raw/master/doc/images/intellisense.png)

In case you do not want to access the dataset but the parent group instead, use the `Get()` method like this:

```cs
var myGroup = bindings.group1.Get();
```

> [!NOTE]
> Invalid characters like spaces will be replaced by underscores.