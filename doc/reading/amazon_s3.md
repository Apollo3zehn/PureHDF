# Amazon S3
| Requires             |
| -------------------- |
| `.NET Standard 2.1+` |

`dotnet add package PureHdf.VFD.AmazonS3`

```cs
using PureHDF.VFD.AmazonS3

// adapt to your needs
var credentials = new AnonymousAWSCredentials();
var region = RegionEndpoint.EUWest1;
var client = new AmazonS3Client(credentials, region);
var s3Stream = new AmazonS3Stream(client, bucketName: "xxx", key: "yyy");

using var file = H5File.Open(s3Stream);

...
```

> [!NOTE]
> The `AmazonS3Stream` caches S3 responses in cache slots of 1 MB by default (use the constructor overload to customize this). Data read from datasets is not being cached to keep the cache small but still useful.