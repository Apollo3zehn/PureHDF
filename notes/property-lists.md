The `DatasetAccess` property list is part of the `NativeDataset.Read(...)` methods instead of being part of the `NativeGroup.Dataset(...)` method which is the case for the original C-library (`H5Dopen2`). There are two reasons:

- It would be difficult / unclean to pass the `DatasetAccess` property list if the dataset is being opened by iterating the result of `IH5Group.Children()`
- None of the `DatasetAccess` properties are required when *opening* the dataset but only when the *data* itself is being *accessed*

