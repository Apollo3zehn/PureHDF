using System;
using System.Collections.Generic;

namespace HDF5.NET
{
    // for later: H5Z_pipeline
    public delegate ulong FilterFunc(uint flags, uint[] parameters, ulong bytesToFilter, ref Span<byte> buffer);

    public static class H5Filter
    {
        static H5Filter()
        {
            H5Filter.Registrations = new List<H5FilterRegistration>();
        }

        internal static List<H5FilterRegistration> Registrations { get; set; }

        public static void Register(int id, string name, FilterFunc filterFunc)
        {
            var registration = new H5FilterRegistration()
            {
                ID = id,
                Name = name,
                FilterFunc = filterFunc
            };

            H5Filter.Registrations.Add(registration);
        }
    }

    internal struct H5FilterRegistration
    {
        public int ID { get; set; }
        //public bool HasEncoder { get; set; }
        //public bool HasDecoder { get; set; }
        public string Name { get; set; }
        //public Func<H5Dataset, bool> CanApply { get; set; }
        //public Action<H5Dataset> SetLocal { get; set; }
        public FilterFunc FilterFunc { get; set; }
    }
}
