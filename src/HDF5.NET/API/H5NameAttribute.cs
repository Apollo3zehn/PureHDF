namespace HDF5.NET
{
    /// <summary>
    /// Specifies the member name that is present in the HDF5 compound data type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class H5NameAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="H5NameAttribute"/> instance.
        /// </summary>
        /// <param name="name">The name of the member.</param>
        public H5NameAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the name of the member.
        /// </summary>
        public string Name { get; set; }
    }
}