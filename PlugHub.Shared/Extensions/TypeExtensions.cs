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
            return JsonSerializer.Serialize(instance, options ?? new JsonSerializerOptions { WriteIndented = true });
        }
    }

}
