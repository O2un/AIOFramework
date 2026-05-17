using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeAnalyzer.Helpers
{
    internal static class DiagnosticReportHelper
    {
        public static void ReportOnNode(this SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor,params object[] messageArgs)
        {
            var diagnostic = Diagnostic.Create(descriptor,context.Node.GetLocation(),messageArgs);

            context.ReportDiagnostic(diagnostic);
        }

        public static void ReportOnSymbol(this SyntaxNodeAnalysisContext context,ISymbol symbol,DiagnosticDescriptor descriptor,params object[] messageArgs)
        {
            var location = symbol.Locations.Length > 0? symbol.Locations[0]: context.Node.GetLocation();

            var diagnostic = Diagnostic.Create(descriptor,location,messageArgs);

            context.ReportDiagnostic(diagnostic);
        }

        public static void ReportOnLocation(this SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] messageArgs)
        {
            var diagnostic = Diagnostic.Create(descriptor, location, messageArgs);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
