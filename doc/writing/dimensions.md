# Dimensions

If you do not specify the dimensions, they will be derived from the data being provided. It the data is scalar, it will also be a scalar in the HDF5 file. If the data is array-like (e.g. `int[,]`), the dimensionality of that array is determined and will be used for the dimensionality of the dataset in the file. You can explicitly specify the dimensionality (or `reshape`) like this:

```cs
// Create a 100x100 dataset - the data itself must be an
// array-like type with a total of 100x100 = 10.000 elements. */
var dataset = new H5Dataset(data, fileDims: [100, 100]);
```

Here is a quick example how to use a multidimensional array to determine the shape of the dataset:

```cs
// Create a 3x3 dataset - no `fileDims` parameter required
var data = new int[,] 
{
    { 0, 1, 2 },
    { 3, 4, 5 },
    { 6, 7, 8 }
};

var dataset = new H5Dataset(data);
```