| Target Type | Target Length | Source Type | Source Length | Action    | Description                                                                              |
| ----------- | ------------- | ----------- | ------------- | --------- | ---------------------------------------------------------------------------------------- |
| value       | 4             | value       | 4             | OK        |                                                                                          |
| value       | 1             | value       | 4             | OK?       | tbd                                                                                      |
| value       | 2             | value       | 4             | exception | this collides with element-wise selections                                               |
| value       | 4             | value       | 1             | exception | this collides with element-wise selections                                               |
| value       | 3             | value       | 4             | exception | this collides with element-wise selections                                               |
| value       | \*            | reference   | \-            | exception | this would allow the user to read global heap Ids                                        |
| value       | \-            | reference   | \-            | OK        | if target = Nullable<ValueType> and source = VLEN with length = 1 and matching type size |
| reference   | \-            | value       | \*            | check     | in GetDecodeInfoForScalar() (e.g. for strings)                                           |
| reference   | \-            | reference   | \-            | check     | in GetDecodeInfoForScalar()                                                              |