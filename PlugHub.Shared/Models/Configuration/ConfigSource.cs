using Microsoft.Extensions.Configuration;
using PlugHub.Shared.Interfaces.Services.Configuration;
using System.Text.Json;

namespace PlugHub.Shared.Models.Configuration
{
    /// <summary>
    /// Represents a configuration source instance that holds configuration data for a specific configuration type.
    /// This class encapsulates the underlying configuration, its location, type information, reload capabilities,
    /// security tokens, and the cached settings values.
    /// </summary>
    public class ConfigSource(IConfigService configService, string sourceLocation, Type sourceType, IConfiguration configuration, Dictionary<string, object?> values, JsonSerializerOptions? jsonOptions, Token ownerToken, Token readToken, Token writeToken, bool reloadOnChanged)
        : IDisposable
    {
        /// <summary>
        /// The configuration service that owns or manages this source.
        /// </summary>
        public IConfigService ConfigService { get; init; } = configService ?? throw new ArgumentNullException(nameof(configService));

        #region ConfigSource: Source Information

        /// <summary>
        /// The type of the configuration object this source provides values for.
        /// </summary>
        public Type SourceType { get; init; } = sourceType;

        /// <summary>
        /// The location or identifier of this configuration source, e.g., a file path or URI.
        /// </summary>
        public string SourceLocation { get; init; } = sourceLocation ?? throw new ArgumentNullException(nameof(sourceLocation));

        /// <summary>
        /// The cached dictionary of configuration values keyed by setting name.
        /// The values are raw deserialized objects representing the config data.
        /// </summary>
        public Dictionary<string, object?> Values { get; set; } = values ?? throw new ArgumentNullException(nameof(values));

        #endregion

        #region ConfigSource: Configuration Information

        /// <summary>
        /// The underlying hierarchical configuration interface representing the data.
        /// May be null if configuration is not hierarchical or unavailable.
        /// </summary>
        public IConfiguration Configuration { get; init; } = configuration ?? throw new ArgumentNullException(nameof(configuration));

        /// <summary>
        /// The JSON serialization options used when serializing or deserializing this configuration source.
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; init; } = jsonOptions ?? new JsonSerializerOptions();

        /// <summary>
        /// Whether this configuration source supports automatic reload on changes.
        /// </summary>
        public bool ReloadOnChanged { get; init; } = reloadOnChanged;

        /// <summary>
        /// Disposable token representing subscription to change notifications on this source.
        /// Setting or disposing this will manage change callback registrations.
        /// </summary>
        public IDisposable? OnChanged { get; set; } = null;

        #endregion

        #region ConfigSource: Access Information

        /// <summary>
        /// Security token representing the owner/administrator of this config source.
        /// </summary>
        public Token Owner { get; init; } = ownerToken;

        /// <summary>
        /// Security token for read access.
        /// </summary>
        public Token Read { get; init; } = readToken;

        /// <summary>
        /// Security token for write access.
        /// </summary>
        public Token Write { get; init; } = writeToken;

        #endregion

        /// <summary>
        /// Releases resources associated with this configuration source.
        /// Disposes any registered change notification subscription.
        /// </summary>
        public void Dispose()
        {
            try
            {
                this.OnChanged?.Dispose();
                this.OnChanged = null;
            }
            catch { /* nothing to see here */ }

            GC.SuppressFinalize(this);
        }
    }
}
