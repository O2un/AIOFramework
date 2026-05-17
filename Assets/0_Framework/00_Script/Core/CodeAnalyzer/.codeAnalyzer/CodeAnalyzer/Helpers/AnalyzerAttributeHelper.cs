using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeAnalyzer.Helpers
{
    internal static class AnalyzerAttributeHelper
    {
        public static bool HasAttribute(ISymbol symbol, string attributeName)
        {
            if (symbol == null)
            {
                return false;
            }

            string attrWithSuffix = attributeName.EndsWith("Attribute") ? attributeName: attributeName + "Attribute";

            string attrWithoutSuffix = attributeName.EndsWith("Attribute") ? attributeName.Substring(0, attributeName.Length - "Attribute".Length) : attributeName;

            foreach (var attribute in symbol.GetAttributes())
            {
                var name = attribute.AttributeClass?.Name;

                if (name == attrWithSuffix || name == attrWithoutSuffix)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasAttributeFullName(ISymbol symbol, string attributeFullName)
        {
            if (symbol == null)
            {
                return false;
            }

            string expectedWithSuffix = attributeFullName.EndsWith("Attribute") ? attributeFullName : attributeFullName + "Attribute";

            string expectedWithoutSuffix = attributeFullName.EndsWith("Attribute") ? attributeFullName.Substring(0, attributeFullName.Length - "Attribute".Length) : attributeFullName;

            foreach (var attribute in symbol.GetAttributes())
            {
                var actualName = attribute.AttributeClass?.ToDisplayString();

                if (actualName == expectedWithSuffix || actualName == expectedWithoutSuffix)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
