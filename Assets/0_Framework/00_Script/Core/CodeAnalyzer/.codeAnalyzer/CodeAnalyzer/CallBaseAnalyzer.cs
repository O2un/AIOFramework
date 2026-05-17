using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;
using CodeAnalyzer.Helpers;

namespace CodeAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CallBaseAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "OE0001",
            title: "base 호출 누락",
            messageFormat: "override된 '{0}' 메서드에서 반드시 base.{0}()를 호출해야 합니다.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodSyntax = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax, context.CancellationToken) as IMethodSymbol;

            if (!BaseOverrideHelper.TryGetBaseMethodWithAttribute(methodSymbol, "MustCallBase", out var baseMethod))
                return;

            bool callsBase = CheckIfBaseCalled(methodSyntax, methodSymbol.Name);

            if (!callsBase)
            {
                var diagnostic = Diagnostic.Create(Rule, methodSyntax.Identifier.GetLocation(), methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool CheckIfBaseCalled(MethodDeclarationSyntax methodSyntax, string methodName)
        {
            var invocations = methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    if (memberAccess.Expression is BaseExpressionSyntax &&
                        memberAccess.Name.Identifier.Text == methodName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
