using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloreriaBautista.Json;

/// <summary>
/// Serializa todos los <see cref="DateTime"/> como UTC en formato ISO 8601 con
/// sufijo 'Z'.
///
/// Motivo: las fechas se guardan con <c>DateTime.UtcNow</c>, pero al leerlas de
/// PostgreSQL su <c>Kind</c> puede quedar <c>Unspecified</c> y System.Text.Json
/// las emite SIN la 'Z'. El frontend (<c>new Date("...sin Z")</c>) las interpreta
/// entonces como hora local y las adelanta +6h (ej. una creacion real de 11:08 a.m.
/// se mostraba como 5:08 p.m.). Marcarlas explicitamente como UTC hace que el
/// navegador convierta correctamente a la hora local.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    // Formato con milisegundos y 'Z' literal para dejar claro que es UTC.
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        // Toda fecha entrante se trata como UTC (Npgsql exige Kind=Utc para
        // columnas "timestamp with time zone").
        return value.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            DateTimeKind.Local       => value.ToUniversalTime(),
            _                        => value,
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            DateTimeKind.Local       => value.ToUniversalTime(),
            _                        => value,
        };
        writer.WriteStringValue(utc.ToString(Format, CultureInfo.InvariantCulture));
    }
}

/// <summary>Variante para <see cref="Nullable{DateTime}"/>.</summary>
public class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return Inner.Read(ref reader, typeof(DateTime), options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        Inner.Write(writer, value.Value, options);
    }
}
