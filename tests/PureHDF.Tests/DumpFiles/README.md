# Dump File Fixtures

These files are expected `h5dump` outputs used by the writing tests.

## `<type>` Placeholder

Many fixtures contain `<type>` where `h5dump` would print the outer object kind:

```hdf5
<type> "Int32[,,]" {
```

Tests replace it with either `DATASET` or `ATTRIBUTE` before comparing the dump output.

This lets dataset and attribute tests share one fixture when the object kind is the only difference.

## File Prefixes

`data_*.dump` is the default fixture family. Attribute tests may reuse these files when the attribute dump has the same datatype, dataspace and data layout as the corresponding dataset dump.

`attr_*.dump` is used when the attribute dump is structurally different from the dataset dump. For example, string attributes may be measured as fixed-length strings while string datasets stay variable-length.

The prefix does not decide whether the file contains `DATASET` or `ATTRIBUTE`. The `<type>` placeholder does that for shared-style fixtures.

## When To Add An `attr_` Fixture

Add an `attr_*.dump` file when the attribute expectation differs from the `data_*.dump` fixture beyond the outer object keyword.

Reuse the `data_*.dump` fixture when replacing `<type>` with `ATTRIBUTE` produces the correct expected output.

For compound values, a string member usually keeps the dataset-style fixed-width behavior because the member width is part of the shared compound datatype. In that case an attribute test can often reuse the `data_*.dump` fixture.
