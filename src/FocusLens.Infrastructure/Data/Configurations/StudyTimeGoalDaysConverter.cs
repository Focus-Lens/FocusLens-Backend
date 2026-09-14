using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FocusLens.Infrastructure.Data.Configurations;

internal static class StudyTimeGoalDaysConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static ValueConverter<IReadOnlyCollection<DayOfWeek>, string> Converter { get; } =
        new(
            days => JsonSerializer.Serialize(days, JsonOptions),
            json => DeserializeDays(json));

    public static ValueComparer<IReadOnlyCollection<DayOfWeek>> Comparer { get; } =
        new(
            (left, right) => left!.SequenceEqual(right!),
            days => days!.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)),
            days => days!.ToArray());

    private static DayOfWeek[] DeserializeDays(string json) =>
        JsonSerializer.Deserialize<DayOfWeek[]>(json, JsonOptions)
        ?? Array.Empty<DayOfWeek>();
}