using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace MaestroNotes.Data.Ai
{
    public static class JsonSchemaHelper
    {
        public static object GenerateSchema(Type type)
        {
            var properties = new Dictionary<string, object>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyName =
                    prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? prop.Name;

                bool mustBeNonNull =
                    propertyName == "Dirigent" ||
                    propertyName == "Orchester";

                object propSchema;

                if (prop.PropertyType == typeof(string))
                {
                    propSchema = mustBeNonNull
                        ? new { type = "string" }
                        : new { type = new object[] { "string", "null" } };
                }
                else if (prop.PropertyType == typeof(string[]))
                {
                    propSchema = mustBeNonNull
                        ? new { type = "array", items = new { type = "string" } }
                        : new { type = new object[] { "array", "null" }, items = new { type = "string" } };
                }
                else
                {
                    propSchema = mustBeNonNull
                        ? new { type = "string" }
                        : new { type = new object[] { "string", "null" } };
                }

                properties[propertyName] = propSchema;
            }

            return new
            {
                type = "object",
                additionalProperties = false,
                properties = properties,
                required = properties.Keys.ToArray() // <- MUSS alle Keys enthalten
            };
        }
    }
}
