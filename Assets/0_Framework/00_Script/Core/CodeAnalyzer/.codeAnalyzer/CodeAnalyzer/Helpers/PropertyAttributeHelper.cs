using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeAnalyzer.Helpers
{
    internal class PropertyAttributeHelper
    {
        public static bool HasAttributeIncludingInterfaceProperty(IPropertySymbol propertySymbol, string attributeName)
        {
            if (propertySymbol == null)
            {
                return false;
            }

            if (AnalyzerAttributeHelper.HasAttribute(propertySymbol, attributeName))
            {
                return true;
            }

            if (HasAttributeOnExplicitInterfaceProperty(propertySymbol, attributeName))
            {
                return true;
            }

            if (HasAttributeOnImplicitInterfaceProperty(propertySymbol, attributeName))
            {
                return true;
            }

            return false;
        }

        private static bool HasAttributeOnExplicitInterfaceProperty(IPropertySymbol propertySymbol, string attributeName)
        {
            foreach (var interfaceProperty in propertySymbol.ExplicitInterfaceImplementations)
            {
                if (AnalyzerAttributeHelper.HasAttribute(interfaceProperty, attributeName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAttributeOnImplicitInterfaceProperty(IPropertySymbol propertySymbol, string attributeName)
        {
            var containingType = propertySymbol.ContainingType;

            if (containingType == null)
            {
                return false;
            }

            foreach (var interfaceType in containingType.AllInterfaces)
            {
                foreach (var interfaceProperty in interfaceType.GetMembers(propertySymbol.Name).OfType<IPropertySymbol>())
                {
                    var implementation = containingType.FindImplementationForInterfaceMember(interfaceProperty);

                    if (!SymbolEqualityComparer.Default.Equals(implementation, propertySymbol))
                    {
                        continue;
                    }

                    if (AnalyzerAttributeHelper.HasAttribute(interfaceProperty, attributeName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
