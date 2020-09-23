using System;
using System.Collections.Generic;
using System.Linq;

namespace HDF5.NET
{
    public delegate Span<byte> FilterFunc(ExtendedFilterFlags flags, uint[] parameters, Span<byte> buffer);

    public static class H5Filter
    {
        static H5Filter()
        {
            H5Filter.Registrations = new List<H5FilterRegistration>();
            H5Filter.Register(FilterIdentifier.Shuffle, "shuffle", H5Filter.ShuffleFilterFunc);
        }

        internal static List<H5FilterRegistration> Registrations { get; set; }

        public static void Register(FilterIdentifier identifier, string name, FilterFunc filterFunc)
        {
            var registration = new H5FilterRegistration(identifier, name, filterFunc);
            H5Filter.Registrations.Add(registration);
        }

        internal static void ExecutePipeline(List<FilterDescription> pipeline,
                                             ExtendedFilterFlags flags,
                                             Span<byte> filterBuffer,
                                             Span<byte> resultBuffer)
        {
            // H5Z.c (H5Z_pipeline)

            /* Read */
            if (flags.HasFlag(ExtendedFilterFlags.Reverse))
            {
                for (int i = pipeline.Count; i > 0; --i)
                {
                    var filter = pipeline[i - 1];
                    var registration = H5Filter.Registrations.FirstOrDefault(current => current.Identifier == filter.Identifier);

                    if (registration == null)
                    {
                        var filterName = string.IsNullOrWhiteSpace(filter.Name) ? "unnamed filter" : filter.Name;
                        throw new Exception($"Could not find filter '{filterName}' with ID '{filter.Identifier}'. Make sure the filter has been registered using H5Filter.Register(...).");
                    }

                    var tmpFlags = (ExtendedFilterFlags)((ushort)flags | (ushort)filter.Flags);

                    try
                    {
                        filterBuffer = registration.FilterFunc(tmpFlags, filter.ClientData, filterBuffer);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Filter pipeline failed.", ex);
                    }
                }

                filterBuffer
                  .Slice(0, resultBuffer.Length)
                  .CopyTo(resultBuffer);
            }
            /* Write */
            else
            {
                throw new Exception("Writing data chunks is not yet supported by HDF5.NET.");
            }
        }

        private static Span<byte> ShuffleFilterFunc(ExtendedFilterFlags flags, uint[] parameters, Span<byte> buffer)
        {
            // read
            if (flags.HasFlag(ExtendedFilterFlags.Reverse))
            {
                var unfilteredBuffer = new byte[buffer.Length];
                ShuffleFilter.Unshuffle((int)parameters[0], buffer, unfilteredBuffer);

                return unfilteredBuffer;
            }

            // write
            else
            {
                var filteredBuffer = new byte[buffer.Length];
                ShuffleFilter.Shuffle((int)parameters[0], buffer, filteredBuffer);

                return filteredBuffer;
            }
        }
    }
}
