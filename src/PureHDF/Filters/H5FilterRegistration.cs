﻿namespace PureHDF
{
    internal class H5FilterRegistration
    {
        public H5FilterRegistration(FilterIdentifier identifier, string name, FilterFunc filterFunc)
        {
            Identifier = identifier;
            Name = name;
            FilterFunc = filterFunc;
        }

        public FilterIdentifier Identifier { get; set; }
        //public bool HasEncoder { get; set; }
        //public bool HasDecoder { get; set; }
        public string Name { get; set; }
        //public Func<H5Dataset, bool> CanApply { get; set; }
        //public Action<H5Dataset> SetLocal { get; set; }
        public FilterFunc FilterFunc { get; set; }
    }
}
