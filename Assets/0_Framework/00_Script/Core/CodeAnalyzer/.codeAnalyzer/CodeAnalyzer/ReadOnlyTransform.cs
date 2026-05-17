using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using CodeAnalyzer.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class ReadOnlyTransform : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "OE0002",
            title: "ReadOnly Transform 조작",
            messageFormat: "ReadOnly Transform을 외부에서 조작하지 마세요",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.Text;

                if (!IsBlockedTransformMethod(methodName))
                {
                    return;
                }

                var targetExpression = memberAccess.Expression;
                var targetType = context.SemanticModel.GetTypeInfo(targetExpression, context.CancellationToken).Type;
                if (!IsUnityTransformType(context.SemanticModel.Compilation, targetType))
                {
                    return;
                }
                var symbolInfo = context.SemanticModel.GetSymbolInfo(targetExpression, context.CancellationToken);
                var symbol = symbolInfo.Symbol;

                if (symbol == null)
                {
                    return;
                }

                if (!HasReadOnlyTransformAttribute(symbol))
                {
                    return;
                }

                var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), symbol.Name, methodName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsUnityTransformType(Compilation compilation, ITypeSymbol typeSymbol)
        {
            var transformType = compilation.GetTypeByMetadataName("UnityEngine.Transform");

            if (transformType == null || typeSymbol == null)
            {
                return false;
            }

            return SymbolEqualityComparer.Default.Equals(typeSymbol, transformType);
        }

        private static bool IsBlockedTransformMethod(string methodName)
        {
            return BlockedTransformMethods.Contains(methodName) || methodName.StartsWith("Set");
        }

        private static readonly HashSet<string> BlockedTransformMethods = new HashSet<string>()
            {
                "SetPositionAndRotation",
                "SetLocalPositionAndRotation",  
                "SetParent",
                "Rotate",
                "RotateAround",
                "Translate",
                "DetachChildren",
                "SetAsFirstSibling",
                "SetAsLastSibling",
                "SetSiblingIndex"
            };

        private void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
        {
            var assignment = (AssignmentExpressionSyntax)context.Node;

            if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
            {
                var propertyName = memberAccess.Name.Identifier.Text;

                if (!IsBlockedTransformProperty(propertyName))
                {
                    return;
                }

                var targetExpression = memberAccess.Expression;
                var symbolInfo = context.SemanticModel.GetSymbolInfo(targetExpression, context.CancellationToken);
                var symbol = symbolInfo.Symbol;

                if (symbol == null)
                {
                    return;
                }

                if (!HasReadOnlyTransformAttribute(symbol))
                {
                    return;
                }

                var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), symbol.Name, propertyName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsBlockedTransformProperty(string propertyName)
        {
            return BlockedTransformProperties.Contains(propertyName);
        }

        private static bool HasReadOnlyTransformAttribute(ISymbol symbol)
        {
            if (symbol is IPropertySymbol propertySymbol)
            {
                return PropertyAttributeHelper.HasAttributeIncludingInterfaceProperty(propertySymbol, "ReadOnlyTransform");
            }

            return AnalyzerAttributeHelper.HasAttribute(symbol, "ReadOnlyTransform");
        }

        private static readonly HashSet<string> BlockedTransformProperties = new HashSet<string>()
            {
                "position",
                "localPosition",
                "rotation",
                "localRotation",
                "eulerAngles",
                "localEulerAngles",
                "localScale",
                "parent"
            };
    }
}
