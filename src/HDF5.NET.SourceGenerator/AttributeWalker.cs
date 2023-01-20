using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HDF5.NET.SourceGenerator;

public class H5SourceGeneratorAttributeCollector : CSharpSyntaxWalker
{
    public override void VisitAttribute(AttributeSyntax node)
    {
        if (nameof(H5SourceGeneratorAttribute).StartsWith(node.Name.ToString()))
            Attributes.Add(node);
    }

    public ICollection<AttributeSyntax> Attributes { get; } = new List<AttributeSyntax>();
}