using System;
using System.Reflection;

namespace HDF5.NET
{
    internal interface IH5DataContainer
    {
        T[] Read<T>() where T : unmanaged;

        T[] ReadCompound<T>() where T : struct;

        T[] ReadCompound<T>(Func<FieldInfo, string> getName) where T : struct;

        string[] ReadString();
    }
}
