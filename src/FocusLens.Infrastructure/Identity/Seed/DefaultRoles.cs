using System.Reflection;
using FocusLens.Domain.Common.Constants;

namespace FocusLens.Infrastructure.Identity.Seed;

public static class DefaultRoles
{
    public static IReadOnlyCollection<string> All { get; } =
    [
        .. typeof(ApplicationRoles)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false }
                            && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
    ];
}