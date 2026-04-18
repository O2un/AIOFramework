using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

namespace CodeGenerator
{
    public static class GeneratorFilterHelper
    {
        // 구문 분석 Syntax Filtering
        public static bool IsClassWithAttribute(SyntaxNode node, CancellationToken _) =>
            node is ClassDeclarationSyntax syntax && syntax.AttributeLists.Count > 0;

        public static bool IsFieldWithAttribute(SyntaxNode node, CancellationToken _) =>
            node is FieldDeclarationSyntax syntax && syntax.AttributeLists.Count > 0;

        public static bool IsMethodWithAttribute(SyntaxNode node, CancellationToken _) =>
            node is MethodDeclarationSyntax syntax && syntax.AttributeLists.Count > 0;

        
        // 의미 분석 Semantic Target Extraction
        public static INamedTypeSymbol GetClassWithAttribute(GeneratorSyntaxContext context, string attributeName)
        {
            if (!(context.Node is ClassDeclarationSyntax classSyntax)) return null;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol;
            return HasAttribute(symbol, attributeName) ? symbol : null;
        }

        public static IFieldSymbol GetFieldWithAttribute(GeneratorSyntaxContext context, string attributeName)
        {
            if (!(context.Node is FieldDeclarationSyntax fieldSyntax)) return null;
            foreach (var variable in fieldSyntax.Declaration.Variables)
            {
                if (context.SemanticModel.GetDeclaredSymbol(variable) is IFieldSymbol symbol)
                {
                    if (HasAttribute(symbol, attributeName)) return symbol;
                }
            }
            return null;
        }

        public static IMethodSymbol GetMethodWithAttribute(GeneratorSyntaxContext context, string attributeName)
        {
            if (!(context.Node is MethodDeclarationSyntax methodSyntax)) return null;
            var symbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;
            return HasAttribute(symbol, attributeName) ? symbol : null;
        }
        
        private static bool HasAttribute(ISymbol symbol, string attributeName)
        {
            if (symbol == null) return false;
            
            string attrWithSuffix = attributeName.EndsWith("Attribute") ? attributeName : attributeName + "Attribute";
            string attrWithoutSuffix = attributeName.EndsWith("Attribute") ? attributeName.Substring(0, attributeName.Length - 9) : attributeName;

            foreach (var attribute in symbol.GetAttributes())
            {
                var name = attribute.AttributeClass?.Name;
                if (name == attrWithSuffix || name == attrWithoutSuffix) return true;
            }
            return false;
        }
    }
}