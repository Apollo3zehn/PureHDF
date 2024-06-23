# External Files

There are multiple mechanisms in HDF5 that allow one file to reference another file. The external file resolution algorithm is specific to each of these mechanisms:

| Type | Documentation | Algorithm | Environment variable |
|---|---|---|---|
| External Link | [Link](https://docs.hdfgroup.org/hdf5/v1_10/group___h5_l.html#title5) | [Link](https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Lpublic.h#L1503-L1566) | `HDF5_EXT_PREFIX` |
| External Dataset Storage | [Link](https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title11) | [Link](https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Ppublic.h#L7084-L7116) | `HDF5_EXTFILE_PREFIX` |
| Virtual Datasets | [Link](https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title12) | [Link](https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Ppublic.h#L6607-L6670) | `HDF5_VDS_PREFIX` |

Usage:

**External Link**

```cs
using PureHDF.VOL.Native;

var group = (NativeGroup)root.Group(...);

var linkAccess = new H5LinkAccess(
    ExternalLinkPrefix: prefix 
);

var dataset = group.Dataset(path, linkAccess);
```

**External Dataset Storage**

```cs
using PureHDF.VOL.Native;

var dataset = (NativeDataset)root.Dataset(...);

var datasetAccess = new H5DatasetAccess(
    ExternalFilePrefix: prefix 
);

var data = dataset.Read<float[]>(..., datasetAccess: datasetAccess);
```

**Virtual Datasets**

```cs
using PureHDF.VOL.Native;

var dataset = (NativeDataset)root.Dataset(...);

var datasetAccess = new H5DatasetAccess(
    VirtualPrefix: prefix 
);

var data = dataset.Read<float[]>(..., datasetAccess: datasetAccess);
```