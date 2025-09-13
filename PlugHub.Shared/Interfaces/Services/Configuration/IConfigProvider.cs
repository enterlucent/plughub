using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Services.Configuration
{
    public interface IConfigProvider
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

        public T GetValue<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null);
        public T GetValue<T>(Type configType, string key, ITokenSet tokenSet);


        public void SetValue<T>(Type configType, string key, T value, Token? ownerToken = null, Token? writeToken = null);
        public void SetValue<T>(Type configType, string key, T value, ITokenSet tokenSet);


        public void SaveValues(Type configType, Token? ownerToken = null, Token? writeToken = null);
        public void SaveValues(Type configType, ITokenSet tokenSet);

        public Task SaveValuesAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default);
        public Task SaveValuesAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default);

        #endregion

        #region IConfigServiceImplementation: Instance Accesors and Mutators

        public object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null);
        public object GetConfigInstance(Type configType, ITokenSet tokenSet);

        public Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default);
        public Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default);

        public void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null);
        public void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet);

        #endregion
    }
}