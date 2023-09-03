| Target Type | Target Length | Source Type | Source Length | Action    | Description                                       |
| ----------- | ------------- | ----------- | ------------- | --------- | ------------------------------------------------- |
| value       | 4             | value       | 4             | OK        |                                                   |
| value       | 1             | value       | 4             | OK?       | tbd                                               |
| value       | 2             | value       | 4             | exception | this collides with element-wise selections        |
| value       | 4             | value       | 1             | exception | this collides with element-wise selections        |
| value       | 3             | value       | 4             | exception | this collides with element-wise selections        |
| value       | \*            | reference   | \-            | exception | this would allow the user to read global heap Ids |
| reference   | -             | value       | \*            | check     | in GetDecodeInfoForScalar() (e.g. for strings)    |
| reference   | \-            | reference   | \-            | check     | in GetDecodeInfoForScalar()                       |