using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PlugHub.Shared.Extensions
{
    public static class TypeExtensions
    {
        private static readonly ConcurrentDictionary<Type, Dictionary<string, MemberInfo>> metadataCache = new();

        /// <summary>
        /// Deterministically converts a CLR type into a stable Guid
        /// by SHA-256 hashing its FullName.
        /// </summary>
        public static Guid ToDeterministicGuid(this Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            // Hash the FullName
            byte[] nameBytes = Encoding.UTF8.GetBytes(type.FullName!);
            byte[] hash = SHA256.HashData(nameBytes);

            // Use the first 16 bytes of the hash to form a GUID
            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, 16).CopyTo(guidBytes);
            return new Guid(guidBytes);
        }

        /// <summary>
        /// Serializes a new instance of the given type (using its parameterless constructor) to JSON.
        /// </summary>
        public static string SerializeToJson(this Type type, JsonSerializerOptions? options = null)
        {
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException($"{type.Name} requires a parameterless constructor for default initialization");
            }

            object instance = Activator.CreateInstance(type)!;
            string serialized = JsonSerializer.Serialize(instance, options);
            return serialized;
        }

        /// <summary>
        /// Retrieves the value of a static public property or field of the given type, using cached metadata if available.
        /// If the value is not cached, it refreshes the cache and tries again.
        /// Throws if the property or field does not exist or is not of the expected type.
        /// </summary>
        /// <typeparam name="T">The expected property or field type.</typeparam>
        /// <param name="type">The type containing the static property or field.</param>
        /// <param name="propertyName">The name of the static property or field.</param>
        /// <returns>The property or field value, or null if not set.</returns>
        /// <exception cref="ArgumentException" />
        public static T? GetStaticPropertyValue<T>(this Type type, string propertyName)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(propertyName);

            if (!metadataCache.TryGetValue(type, out Dictionary<string, MemberInfo>? memberInfos) || !memberInfos.TryGetValue(propertyName, out MemberInfo? member))
            {
                type.GetStaticProperties();
                if (!metadataCache.TryGetValue(type, out memberInfos) || !memberInfos.TryGetValue(propertyName, out member))
                {
                    throw new ArgumentException(
                        $"The property or field '{propertyName}' does not exist on type '{type.FullName}' or its ancestors.",
                        nameof(propertyName));
                }
            }

            object? value = member switch
            {
                FieldInfo field => field.GetValue(null),
                PropertyInfo prop => prop.GetValue(null),
                _ => null
            };

            if (value == null)
            {
                return default;
            }

            if (!typeof(T).IsAssignableFrom(value.GetType()))
            {
                throw new ArgumentException(
                    $"The property or field '{propertyName}' is not of type {typeof(T).Name}.",
                    nameof(propertyName));
            }

            return (T)value;
        }

        /// <summary>
        /// Extracts static public fields and properties from <paramref name="sourceType"/> and returns them
        /// as a dictionary of parameter name to value. This is useful for plugin metadata extraction
        /// where all data is static and documented on a base class.
        /// </summary>
        /// <param name="sourceType">The type to extract static metadata from.</param>
        /// <returns>A dictionary mapping static member names to their values.</returns>
        public static Dictionary<string, object?> GetStaticProperties(this Type sourceType)
        {
            ArgumentNullException.ThrowIfNull(sourceType);

            Dictionary<string, MemberInfo> mapping = metadataCache.GetOrAdd(sourceType, type =>
            {
                return type
                    .GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Where(m => m.MemberType is MemberTypes.Property or MemberTypes.Field)
                    .ToDictionary(m => m.Name, m => m, StringComparer.OrdinalIgnoreCase);
            });

            Dictionary<string, object?> result = new(mapping.Count, StringComparer.OrdinalIgnoreCase);

            foreach ((string name, MemberInfo member) in mapping)
            {
                object? value = member switch
                {
                    FieldInfo field => field.GetValue(null),
                    PropertyInfo prop => prop.GetValue(null),
                    _ => null
                };

                result[name] = value;
            }

            return result;
        }
    }
}