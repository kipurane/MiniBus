using System.Linq;
using Microsoft.CodeAnalysis;

namespace MiniBus.Analyzers;

internal static class MiniBusSymbolExtensions
{
    public static bool Implements(this ITypeSymbol type, INamedTypeSymbol? contract)
    {
        if (contract is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type, contract))
        {
            return true;
        }

        return type.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, contract));
    }

    public static bool ImplementsGeneric(this ITypeSymbol type, INamedTypeSymbol? genericContract)
    {
        if (genericContract is null)
        {
            return false;
        }

        return type.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, genericContract));
    }

    public static bool InheritsFromGeneric(this ITypeSymbol type, INamedTypeSymbol? genericBaseType)
    {
        if (genericBaseType is null)
        {
            return false;
        }

        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, genericBaseType))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsOrInheritsFrom(this ITypeSymbol type, INamedTypeSymbol? baseType)
    {
        if (baseType is null)
        {
            return false;
        }

        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

}
