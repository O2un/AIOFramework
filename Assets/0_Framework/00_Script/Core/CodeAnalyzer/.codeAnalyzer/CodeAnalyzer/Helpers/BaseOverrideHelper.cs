using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeAnalyzer.Helpers
{
    internal static class BaseOverrideHelper
    {
        public static bool TryGetBaseMethodWithAttribute(IMethodSymbol methodSymbol, string attributeName, out IMethodSymbol baseMethod)
        {
            baseMethod = null;

            if (methodSymbol == null)
            {
                return false;
            }

            if (!methodSymbol.IsOverride)
            {
                return false;
            }

            baseMethod = methodSymbol.OverriddenMethod;

            if (baseMethod == null)
            {
                return false;
            }

            if (!AnalyzerAttributeHelper.HasAttribute(baseMethod, attributeName))
            {
                return false;
            }

            return true;
        }

        public static bool TryGetBasePropertyWithAttribute(IPropertySymbol propertySymbol, string attributeName, out IPropertySymbol baseProperty)
        {
            baseProperty = null;

            if (propertySymbol == null)
            {
                return false;
            }

            if (!propertySymbol.IsOverride)
            {
                return false;
            }

            baseProperty = propertySymbol.OverriddenProperty;

            if (baseProperty == null)
            {
                return false;
            }

            if (!AnalyzerAttributeHelper.HasAttribute(baseProperty, attributeName))
            {
                return false;
            }

            return true;
        }
    }
}
