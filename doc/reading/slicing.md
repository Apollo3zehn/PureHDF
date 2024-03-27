# Slicing

## Overview

Data selection is one of the strengths of HDF5 and is applicable to all dataset types (contiguous, compact and chunked). With PureHDF, the full dataset can be read with a simple call to `dataset.Read<T>()`. However, if you want to read only parts of the dataset, [selections](https://support.hdfgroup.org/HDF5/Tutor/selectsimple.html) are your friend. 

PureHDF supports three types of selections. These are:

| Type                 | Description                                                                                                                                                                      |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `HyperslabSelection` | A hyperslab is a selection of elements from a hyper rectangle.<br />[HDF5 User's Guide](https://portal.hdfgroup.org/display/HDF5/HDF5+User+Guides) > 7.4.1.1 Hyperslab Selection |
| `PointSelection`     | Selects a collection of points.<br />[HDF5 User's Guide](https://portal.hdfgroup.org/display/HDF5/HDF5+User+Guides) > 7.4.1.2 Select Points                                      |
| `DelegateSelection`  | This selection accepts a custom walker which selects the user defined points or blocks.                                                                                          |

## Examples

Selections can be passed to the read method to avoid reading the full dataset like this:

```cs
var fileSelection = ...;
var data = dataset.Read<int[]>(fileSelection: fileSelection);
```

Alternatively, if the selection should not be applied to the file but to the memory buffer, use the `memorySelection` parameter:

```cs
var memorySelection = ...;
var data = dataset.Read<int[]>(memorySelection: memorySelection);
```

All parameters are optional. For example, when the `fileSelection` parameter is ommited, the whole dataset will be read. Note that the number of data points in the file selection must always match that of the memory selection.

> [!NOTE]
> There are overload methods that allow you to provide your own buffer.

**Point selection**

Point selections require a two-dimensional `n` x `m` array where `n` is the number of points and `m` the rank of the dataset. Here is an example with four points to select data from a dataset of rank = `3`.

```cs
using PureHDF.Selections;

var selection = new PointSelection(new ulong[,] {
    { 00, 00, 00 },
    { 00, 05, 10 },
    { 12, 01, 10 },
    { 05, 07, 09 }
});
```

**Hyperslab selection**

A hyperslab selection can be used to select a contiguous block of elements or to select multiple blocks.

The simplest example is a selection for a 1-dimensional dataset at a certain offset (`start: 10`) and a certain length (`block: 50`):

```cs
var fileSelection = new HyperslabSelection(start: 10, block: 50);
```

The following - more advanced - example shows selections for a three-dimensional dataset (source) and a two-dimensional memory buffer (target):

```cs
var dataset = root.Dataset("myDataset");
var memoryDims = new ulong[] { 75, 25 };

var datasetSelection = new HyperslabSelection(
    rank: 3,
    starts: [2, 2, 0],
    strides: [5, 8, 2],
    counts: [5, 3, 2],
    blocks: [3, 5, 2]
);

var memorySelection = new HyperslabSelection(
    rank: 2,
    starts: [2, 1],
    strides: [35, 17],
    counts: [2, 1],
    blocks: [30, 15]
);

var result = dataset
    .Read<int[,]>(
        fileSelection: datasetSelection,
        memorySelection: memorySelection,
        memoryDims: memoryDims
    );
``` 

**Delegate selection**

A delegate accepts a custom walker function which select blocks of data at certain coordinates. Here is an example which selects a total number of 11 elements from a 3-dimensional dataset:

```cs
static IEnumerable<Step> Walker(ulong[] datasetDimensions)
{
    yield return new Step(Coordinates: [00, 00, 00], ElementCount: 1);
    yield return new Step(Coordinates: [00, 05, 10], ElementCount: 5);
    yield return new Step(Coordinates: [12, 01, 10], ElementCount: 2);
    yield return new Step(Coordinates: [05, 07, 09], ElementCount: 3);
};

var selection = new DelegateSelection(totalElementCount: 11, Walker);
```