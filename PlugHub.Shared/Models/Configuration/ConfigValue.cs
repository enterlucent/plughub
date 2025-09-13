namespace PlugHub.Shared.Models.Configuration
{
    /// <summary>
    /// Represents a single configuration setting's metadata and its stored value.
    /// </summary>
    public class ConfigValue
    {
        /// <summary>
        /// The .NET type of the setting's value.
        /// Defaults to <see cref="string"/> if not specified.
        /// </summary>
        public Type ValueType { get; set; } = typeof(string);

        /// <summary>
        /// The stored value of the setting.
        /// Can be null if no value is set.
        /// </summary>
        public object? Value { get; set; } = null;

        /// <summary>
        /// Indicates whether the setting value can be read.
        /// Defaults to true.
        /// </summary>
        public bool CanRead { get; set; } = true;

        /// <summary>
        /// Indicates whether the setting value can be written to.
        /// Defaults to false.
        /// </summary>
        public bool CanWrite { get; set; } = false;
    }
}