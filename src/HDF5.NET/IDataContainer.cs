using System;
using System.Reflection;

namespace HDF5.NET
{
    internal interface IDataContainer
    {
        T[] Read<T>() where T : struct;

        T[] ReadCompound<T>() where T : struct;

        T[] ReadCompound<T>(Func<FieldInfo, string> getName) where T : struct;

        string[] ReadString();
    }
}
