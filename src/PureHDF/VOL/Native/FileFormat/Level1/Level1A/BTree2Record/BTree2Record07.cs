﻿// using System;

// namespace PureHDF.VOL.Native;

// internal struct BTree2Record07 : IBTree2Record
// {
//     #region Constructors

//     public BTree2Record07(MessageLocation messageLocation)
//     {
//         MessageLocation = messageLocation;
//     }

//     #endregion

//     #region Properties

//     public MessageLocation MessageLocation { get; }

//     #endregion

//     #region Properties

//     public static BTree2Record07 Construct(H5Context context)
//     {
//         var messageLocation = (MessageLocation)driver.ReadByte();

//         return messageLocation switch
//         {
//             MessageLocation.Heap            => new BTree2Record07_0(driver, messageLocation),
//             MessageLocation.ObjectHeader    => new BTree2Record07_1(driver, superblock, messageLocation),
//             _                               => throw new Exception($"Unknown message location '{MessageLocation.Heap}'.")
//         };
//     }

//     #endregion
// }