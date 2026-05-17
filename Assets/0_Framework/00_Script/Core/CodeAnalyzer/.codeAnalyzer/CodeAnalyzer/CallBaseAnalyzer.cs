using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace CodeAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CallBaseAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "MY0001",
            title: "base 호출 누락",
            messageFormat: "override된 '{0}' 메서드에서 반드시 base.{0}()를 호출해야 합니다.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error, // 여기서 Error로 설정하면 빌드 자체가 안 됩니다!
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodSyntax = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;

            if (methodSymbol == null || !methodSymbol.IsOverride) return;

            var baseMethod = methodSymbol.OverriddenMethod;
            var hasCallBase = baseMethod.GetAttributes().Any(ad => ad.AttributeClass.Name == "CallBaseAttribute");
            if (!hasCallBase) return;

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
