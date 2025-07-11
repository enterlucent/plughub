using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Services
{
    public interface IConfigServiceProvider
    {
        public IEnumerable<Type> SupportedParamsTypes { get; init; }
        public Type RequiredAccessorInterface { get; init; }

        #region IConfigServiceImplementation: Registration

        public void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService configService);
        void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams, IConfigService service);

        public void UnregisterConfig(Type configType, Token? ownerToken = null);
        public void UnregisterConfig(Type configType, ITokenSet tokenSet);

        public void UnregisterConfigs(IEnumerable<Type> configTypes, Token? ownerToken = null);
        public void UnregisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet);

        #endregion

        #region IConfigServiceImplementation: Value Accessors and Mutators

        public T GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null);
        public T GetDefault<T>(Type configType, string key, ITokenSet tokenSet);

        public T GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null);
        public T GetSetting<T>(Type configType, string key, ITokenSet tokenSet);


        public void SetDefault<T>(Type configType, string key, T value, Token? ownerToken = null, Token? writeToken = null);
        public void SetDefault<T>(Type configType, string key, T value, ITokenSet tokenSet);

        public void SetSetting<T>(Type configType, string key, T value, Token? ownerToken = null, Token? writeToken = null);
        public void SetSetting<T>(Type configType, string key, T value, ITokenSet tokenSet);


        public void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null);
        public void SaveSettings(Type configType, ITokenSet tokenSet);

        public Task SaveSettingsAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default);
        public Task SaveSettingsAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default);

        #endregion

        #region IConfigServiceImplementation: Instance Accesors and Mutators

        public object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null);
        public object GetConfigInstance(Type configType, ITokenSet tokenSet);

        public Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default);
        public Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default);

        public void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null);
        public void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet);

        #endregion

        #region IConfigServiceImplementation: Default Config Mutation/Migration

        public string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null);
        public string GetDefaultConfigFileContents(Type configType, ITokenSet tokenSet);

        public Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, Token? ownerToken = null, CancellationToken cancellationToken = default);
        public Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, ITokenSet tokenSet, CancellationToken cancellationToken = default);

        public void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null);
        public void SaveDefaultConfigFileContents(Type configType, string contents, ITokenSet tokenSet);

        #endregion
    }
}