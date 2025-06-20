using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlugHub.Services
{
    public class ConfigAccessor(IConfigService service, IEnumerable<Type> configTypes, Token readToken, Token writeToken) : IConfigAccessor, IDisposable
    {
        private readonly IConfigService service = service;
        private readonly IEnumerable<Type> configTypes = configTypes;
        private readonly Token readToken = readToken;
        private readonly Token writeToken = writeToken;

        public IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            if (!this.configTypes.Contains(typeof(TConfig)))
            {
                throw new TypeAccessException(
                    $"Configuration type {typeof(TConfig).Name} is not accessible through this accessor. " +
                    $"Registered types: {string.Join(", ", this.configTypes.Select(t => t.Name))}"
                );
            }

            return new ConfigAccessorFor<TConfig>(this.service, this.readToken, this.writeToken);
        }

        public void Dispose()
        {
        }
    }

    public class ConfigAccessorFor<TConfig>(IConfigService service, Token readToken, Token writeToken) : IDisposable, IConfigAccessorFor<TConfig> where TConfig : class
    {
        private readonly IConfigService service = service;
        private readonly Token readToken = readToken;
        private readonly Token writeToken = writeToken;


        TConfig IConfigAccessorFor<TConfig>.Get()
        {
            var instance = this.service.GetConfigInstance(typeof(TConfig), this.readToken);
            return instance as TConfig
                ?? throw new InvalidCastException($"Invalid config type for {typeof(TConfig)}");
        }

        async Task IConfigAccessorFor<TConfig>.SaveAsync(TConfig config)
        {
            this.service.SaveConfigInstance(typeof(TConfig), config, this.writeToken);

            await this.service.SaveSettingsAsync(typeof(TConfig), this.writeToken);
        }

        T IConfigAccessorFor<TConfig>.Get<T>(string key)
            => this.service.GetSetting<T>(typeof(TConfig), key, this.readToken);

        void IConfigAccessorFor<TConfig>.Set<T>(string key, T value)
            => this.service.SetSetting(typeof(TConfig), key, value, this.writeToken);

        async Task IConfigAccessorFor<TConfig>.SaveAsync()
            => await this.service.SaveSettingsAsync(typeof(TConfig), this.writeToken);

        public void Dispose()
        {
        }
    }
}
