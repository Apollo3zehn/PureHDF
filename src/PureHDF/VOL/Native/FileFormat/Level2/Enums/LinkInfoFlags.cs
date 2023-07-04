﻿namespace PureHDF.VOL.Native;

[Flags]
internal enum LinkInfoFlags : byte
{
    NoFlags = 0,
    LinkNameLengthSizeLowerBit = 1,
    LinkNameLengthSizeUpperBit = 2,
    CreatOrderFieldIsPresent = 4,
    LinkTypeFieldIsPresent = 8,
    LinkNameEncodingFieldIsPresent = 16
}