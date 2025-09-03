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

            byte[] nameBytes = Encoding.UTF8.GetBytes(type.FullName ?? string.Empty);
            byte[] hash = SHA256.HashData(nameBytes);

            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, 16).CopyTo(guidBytes);
            return new Guid(guidBytes);
        }

        /// <summary>
        /// Serializes a new instance of the given type (using its parameterless constructor) to JSON.
        /// </summary>
        public static string SerializeToJson(this Type type, JsonSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(type);

            bool hasParameterlessConstructor = type.GetConstructor(Type.EmptyTypes) != null;

            if (!hasParameterlessConstructor)
            {
                throw new InvalidOperationException($"{type.Name} requires a parameterless constructor for default initialization");
            }

            object instance = Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Failed to create an instance of {type.Name}.");
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

            bool memberFoundInCache = metadataCache.TryGetValue(type, out Dictionary<string, MemberInfo>? memberInfos);
            MemberInfo? member;
            bool memberFoundAfterRefresh;

            if (memberFoundInCache && memberInfos != null)
            {
                memberFoundAfterRefresh = memberInfos.TryGetValue(propertyName, out member);
                if (memberFoundAfterRefresh && member != null)
                {
                    return GetValueFromMember<T>(propertyName, member);
                }
            }

            memberInfos = GetOrAddMetadataCache(type);
            memberFoundAfterRefresh = memberInfos.TryGetValue(propertyName, out member);

            if (memberFoundAfterRefresh && member != null)
            {
                return GetValueFromMember<T>(propertyName, member);
            }

            throw new ArgumentException(
                $"The property or field '{propertyName}' does not exist on type '{type.FullName}' or its ancestors.",
                nameof(propertyName));
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

            Dictionary<string, MemberInfo> mapping = GetOrAddMetadataCache(sourceType);
            Dictionary<string, object?> result = new(mapping.Count, StringComparer.OrdinalIgnoreCase);

            foreach ((string name, MemberInfo member) in mapping)
            {
                object? value = ExtractValueFromMember(member);
                result[name] = value;
            }

            return result;
        }

        #region TypeExtensions: Helper Methods

        private static Dictionary<string, MemberInfo> GetOrAddMetadataCache(Type type)
        {
            return metadataCache.GetOrAdd(type, t =>
            {
                Dictionary<string, MemberInfo> members = t.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                               .Where(m => m.MemberType is MemberTypes.Property or MemberTypes.Field)
                               .ToDictionary(m => m.Name, m => m, StringComparer.OrdinalIgnoreCase);
                return members;
            });
        }

        private static T? GetValueFromMember<T>(string propertyName, MemberInfo member)
        {
            object? value = ExtractValueFromMember(member);

            if (value == null)
            {
                return default;
            }

            bool isCorrectType = typeof(T).IsAssignableFrom(value.GetType());

            if (!isCorrectType)
            {
                throw new ArgumentException($"The property or field '{propertyName}' is not of type {typeof(T).Name}.", nameof(propertyName));
            }

            return (T)value;
        }

        private static object? ExtractValueFromMember(MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => field.GetValue(null),
                PropertyInfo prop => prop.GetValue(null),
                _ => null
            };
        }

        #endregion
    }
}