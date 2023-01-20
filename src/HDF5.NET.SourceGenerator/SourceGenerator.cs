using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HDF5.NET.SourceGenerator;

[Generator]
public class SourceGenerator : ISourceGenerator
{
    private record SourceGeneratorItem(string FullQualifiedClassName, string FilePath);

    public void Execute(GeneratorExecutionContext context)
    {
        // find attribute
        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var allClasses = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>();

            var items = new List<SourceGeneratorItem>();

            foreach (var currentClass in allClasses)
            {
                var name = currentClass.Identifier.ToFullString();
                var fullQualifiedClassName = name;

                var attributeSyntax = currentClass.AttributeLists
                    .SelectMany(attributeList => attributeList.Attributes)
                    .Where(attribute => attribute.Name.NormalizeWhitespace().ToFullString() == nameof(H5SourceGeneratorAttribute))
                    .FirstOrDefault();

                if (name != "SourceGeneratorTests")
                    throw new Exception(name);

                if (attributeSyntax is null)
                    continue;

                var firstArgument = attributeSyntax.ArgumentList!.Arguments.First();
                var argumentFullString = firstArgument.NormalizeWhitespace().ToFullString();
                var argumentExpression = firstArgument.Expression.NormalizeWhitespace().ToFullString();
                var filePath = argumentExpression;

                throw new Exception(fullQualifiedClassName);

                items.Add(new SourceGeneratorItem(fullQualifiedClassName, filePath));
            }

            throw new Exception("nothing");
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        // No initialization required for this one
    }

    // https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/
    private static string GetNamespace(BaseTypeDeclarationSyntax syntax)
    {
        string namespaceString = string.Empty;

        var potentialNamespaceParent = syntax.Parent;
        
        while (potentialNamespaceParent != null &&
            potentialNamespaceParent is not NamespaceDeclarationSyntax
            && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            namespaceString = namespaceParent.Name.ToString();
            
            while (true)
            {
                if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
                    break;

                namespaceString = $"{namespaceParent.Name}.{namespaceString}";
                namespaceParent = parent;
            }
        }

        return namespaceString;
    }
}