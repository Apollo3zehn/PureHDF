# Highly Scalable Data Service (HSDS)
| Requires  |
| --------- |
| `.NET 6+` |

The `HsdsConnector` shown below uses the `HsdsClient` from the [hsds-api](https://github.com/Apollo3zehn/hsds-api) project. Please follow that link for information about authentication.

```cs
var domainName = "/shared/tall.h5";
var client = new HsdsClient(new Uri("http://hsdshdflab.hdfgroup.org"));
var root = HsdsConnector.Create(domainName, client);
var group = root.Group("/my-group");

...
```

The following features are not (yet) implemented:
- `IH5Attribute`
  - Read compounds
  - Read strings and other variable-length data types
- `IH5Dataset`:
  - Read compounds
  - Read strings and other variable-length data types
  - Memory selections
- `IH5DataType`:
  - `Size` property (the HSDS REST API does not seem to provide that information)
  - data type properties other than `integer`, `float` and `compound`
- `IH5FillValue`

**Please file a new issue if you encounter any problems.**