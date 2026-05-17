using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeAnalyzer.Helpers
{
    internal static class AnalyzerSymbolHelper
    {
        public static INamedTypeSymbol GetClassSymbol(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ClassDeclarationSyntax syntax)
            {
                return context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken) as INamedTypeSymbol;
            }

            return null;
        }

        public static IMethodSymbol GetMethodSymbol(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is MethodDeclarationSyntax syntax)
            {
                return context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken) as IMethodSymbol;
            }
            return null;
        }

        public static IEnumerable<IFieldSymbol> GetFieldSymbols(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is FieldDeclarationSyntax syntax)
            {
                foreach (var variable in syntax.Declaration.Variables)
                {
                    var symbol = context.SemanticModel.GetDeclaredSymbol(variable,context.CancellationToken) as IFieldSymbol;

                    if (symbol != null)
                    {
                        yield return symbol;
                    }
                }
            }
        }

        public static IPropertySymbol GetPropertySymbol(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is PropertyDeclarationSyntax syntax)
            {
                return context.SemanticModel.GetDeclaredSymbol(syntax,context.CancellationToken) as IPropertySymbol;
            }

            return null;
        }
    }
}
