using System;

namespace HDF5.NET
{
    [AttributeUsage(AttributeTargets.Field)]
    public class H5NameAttribute : Attribute
    {
        public H5NameAttribute(string name)
        {
            this.Name = name;
        }

        public string Name { get; set; }
    }
}