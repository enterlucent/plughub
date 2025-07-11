using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PlugHub.Shared.Extensions
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Serializes a new instance of the given type (using its parameterless constructor) to JSON.
        /// </summary>
        public static string SerializeToJson(this Type type, JsonSerializerOptions? options = null)
        {
            if (type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"{type.Name} requires a parameterless constructor for default initialization");

            object instance = Activator.CreateInstance(type)!;
            string serialized = JsonSerializer.Serialize(instance, options);
            return serialized;
        }

        /// <summary>
        /// Retrieves the value of a static public property of the given type, cast to the specified type T.
        /// </summary>
        /// <typeparam name="T">The expected property type.</typeparam>
        /// <param name="type">The type containing the static property.</param>
        /// <param name="propertyName">The name of the static property.</param>
        /// <returns>The property value, or null if not set.</returns>
        /// <exception cref="ArgumentException"/>
        public static T? GetStaticPropertyValue<T>(this Type type, string propertyName)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(propertyName);

            Type? currentType = type;
            PropertyInfo? property = null;

            while (currentType != null)
            {
                property = currentType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (property != null)
                    break;
                currentType = currentType.BaseType;
            }

            if (property == null)
                throw new ArgumentException($"The property '{propertyName}' does not exist on type '{type.FullName}' or its ancestors.", nameof(propertyName));
            if (!typeof(T).IsAssignableFrom(property.PropertyType))
                throw new ArgumentException($"The property '{propertyName}' is not of type {typeof(T).Name}.", nameof(propertyName));

            return (T?)property.GetValue(null);
        }

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
    }
}