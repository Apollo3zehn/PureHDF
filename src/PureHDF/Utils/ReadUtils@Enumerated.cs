namespace PureHDF;

internal static partial class ReadUtils
{
    private static Array? ReadEnumerated(NativeReadContext context, DatatypeMessage type, Span<byte> slicedData)
    {
        if (type.Class != DatatypeMessageClass.Enumerated)
            throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Enumerated}'.");

        var properties = (EnumerationPropertyDescription)type.Properties[0];
        var baseType = properties.BaseType;

        return ReadRawArray(context, baseType, slicedData);
    }
}