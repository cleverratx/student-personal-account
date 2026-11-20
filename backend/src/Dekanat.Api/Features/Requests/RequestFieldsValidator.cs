using System.Globalization;
using System.Text.Json;

namespace Dekanat.Api.Features.Requests
{
    /// <param name="Json">Поля, очищенные и приведённые к виду для базы. Пусто, если есть ошибки.</param>
    /// <param name="Errors">Ошибки по полям, ключ вида fields.destination. Пусто, если всё в порядке.</param>
    public sealed record FieldsValidation( string? Json, Dictionary<string, string[]> Errors );

    /// <summary>
    /// Проверяет значения полей заявки по схеме её типа (<see cref="RequestType.FieldSchema"/>). Лишние поля не принимаются,
    /// строки обрезаются по краям, пустая строка считается незаполненным полем. Тексты ошибок по-русски, с названием поля.
    /// </summary>
    public static class RequestFieldsValidator
    {
        private const int DefaultMaxLength = 500;
        private const int MaxUnknownNames = 5;
        private const int MaxNameLength = 40;

        public static FieldsValidation Validate( JsonElement input, string fieldSchema )
        {
            if ( input.ValueKind != JsonValueKind.Object )
            {
                return new FieldsValidation( null, new Dictionary<string, string[]> { [ "fields" ] = [ "Передайте поля заявки объектом" ] } );
            }

            using JsonDocument schemaDocument = JsonDocument.Parse( fieldSchema );
            JsonElement schema = schemaDocument.RootElement;
            Dictionary<string, string[]> errors = [];
            Dictionary<string, object> clean = [];

            foreach ( JsonProperty definition in schema.EnumerateObject() )
            {
                string name = definition.Name;
                JsonElement rule = definition.Value;
                string title = rule.GetProperty( "title" ).GetString()!;

                if ( !IsFilled( input, name, out JsonElement value ) )
                {
                    if ( rule.GetProperty( "required" ).GetBoolean() )
                    {
                        errors[ $"fields.{name}" ] = [ $"Заполните поле «{title}»" ];
                    }

                    continue;
                }

                (string? error, object? normalized) = Check( rule, title, value );
                if ( error is not null )
                {
                    errors[ $"fields.{name}" ] = [ error ];
                }
                else
                {
                    clean[ name ] = normalized!;
                }
            }

            string[] unknown =
            [
                .. input.EnumerateObject()
                    .Select( property => property.Name )
                    .Where( name => !schema.TryGetProperty( name, out _ ) )
                    .Distinct()
                    .Take( MaxUnknownNames )
                    .Select( name => name.Length > MaxNameLength ? name[ ..MaxNameLength ] + "…" : name ),
            ];
            if ( unknown.Length > 0 )
            {
                errors[ "fields" ] = [ $"В этой заявке нет полей: {string.Join( ", ", unknown )}" ];
            }

            return errors.Count > 0
                ? new FieldsValidation( null, errors )
                : new FieldsValidation( JsonSerializer.Serialize( clean ), errors );
        }

        private static bool IsFilled( JsonElement input, string name, out JsonElement value )
        {
            if ( !input.TryGetProperty( name, out value ) )
            {
                return false;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => false,
                JsonValueKind.String => !string.IsNullOrWhiteSpace( value.GetString() ),
                _ => true,
            };
        }

        private static (string? Error, object? Value) Check( JsonElement rule, string title, JsonElement value )
        {
            return rule.GetProperty( "type" ).GetString() switch
            {
                "string" => CheckString( rule, title, value ),
                "integer" => CheckInteger( rule, title, value ),
                "date" => CheckDate( title, value ),
                "uuid" => CheckUuid( title, value ),
                "choice" => CheckChoice( rule, title, value ),
                _ => (Wrong( title ), null),
            };
        }

        private static (string? Error, object? Value) CheckString( JsonElement rule, string title, JsonElement value )
        {
            if ( value.ValueKind != JsonValueKind.String )
            {
                return (Wrong( title ), null);
            }

            string text = value.GetString()!.Trim();
            int max = Bound( rule, "maxLength" ) ?? DefaultMaxLength;
            return text.Length > max ? ($"В поле «{title}» не больше {max} символов", null) : (null, text);
        }

        private static (string? Error, object? Value) CheckInteger( JsonElement rule, string title, JsonElement value )
        {
            int? min = Bound( rule, "min" );
            int? max = Bound( rule, "max" );
            if ( value.ValueKind != JsonValueKind.Number || !value.TryGetInt32( out int number ) || number < min || number > max )
            {
                string range = min is not null && max is not null ? $" от {min} до {max}" : "";
                return ($"В поле «{title}» укажите целое число{range}", null);
            }

            return (null, number);
        }

        private static (string? Error, object? Value) CheckDate( string title, JsonElement value )
        {
            if ( value.ValueKind == JsonValueKind.String
                && DateOnly.TryParseExact( value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date ) )
            {
                return (null, date.ToString( "yyyy-MM-dd", CultureInfo.InvariantCulture ));
            }

            return ($"В поле «{title}» укажите дату в формате ГГГГ-ММ-ДД", null);
        }

        private static (string? Error, object? Value) CheckUuid( string title, JsonElement value )
        {
            if ( value.ValueKind == JsonValueKind.String && Guid.TryParse( value.GetString(), out Guid id ) && id != Guid.Empty )
            {
                return (null, id.ToString( "D" ));
            }

            return ($"Выберите значение в поле «{title}»", null);
        }

        private static (string? Error, object? Value) CheckChoice( JsonElement rule, string title, JsonElement value )
        {
            string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            bool known = text is not null
                && rule.GetProperty( "options" ).EnumerateArray().Any( option => option.GetProperty( "value" ).GetString() == text );
            return known ? (null, text) : ($"Выберите вариант из списка в поле «{title}»", null);
        }

        private static int? Bound( JsonElement rule, string name )
        {
            return rule.TryGetProperty( name, out JsonElement bound ) && bound.TryGetInt32( out int number ) ? number : null;
        }

        private static string Wrong( string title )
        {
            return $"Неверное значение в поле «{title}»";
        }
    }
}
