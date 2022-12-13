namespace HDF5.NET
{
    public delegate Memory<byte> FilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer);

    public static partial class H5Filter
    {
        public static void Register(H5FilterID identifier, string name, FilterFunc filterFunc)
        {
            var registration = new H5FilterRegistration((FilterIdentifier)identifier, name, filterFunc);
            H5Filter.Registrations.AddOrUpdate((FilterIdentifier)identifier, registration, (_, oldRegistration) => registration);
        }
    }
}
