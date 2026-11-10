using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskFlow.Infrastructure.Data.Configurations;

internal static class SqlServerUtcConverters
{
    public static readonly ValueConverter<DateTime, DateTime> DateTime =
        new(value => value, value => System.DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> NullableDateTime =
        new(value => value, value => value.HasValue
            ? System.DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null);
}
