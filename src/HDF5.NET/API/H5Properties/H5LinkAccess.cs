namespace HDF5.NET
{
    /// <summary>
    /// A structure which controls how the link is accessed. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___l_a_p_l.html">hdfgroup.org</seealso>.
    /// </summary>
    public struct H5LinkAccess
    {
        /// <summary>
        /// Gets the prefix to be applied to external link paths. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___l_a_p_l.html#gafa5eced13ba3a00cdd65669626dc7294">hdfgroup.org</seealso>.
        /// </summary>
        public string ExternalLinkPrefix { get; init; }
    }
}
