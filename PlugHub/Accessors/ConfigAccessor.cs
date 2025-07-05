using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Accessors
{
    public class ConfigAccessor(ILogger<IConfigAccessor> logger, IConfigService configService) : IConfigAccessor
    {
        protected readonly ILogger<IConfigAccessor> Logger = logger;
        protected readonly IConfigService ConfigService = configService;
        protected readonly List<Type> ConfigTypes = [];

        protected bool Initialized;

        protected Token? OwnerToken;
        protected Token? ReadToken;
        protected Token? WriteToken;

        public IConfigAccessor Init(IList<Type> configTypes, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        {
            if (this.Initialized)
                throw new InvalidOperationException("Accessor already initialised");

            this.OwnerToken = ownerToken;
            this.ReadToken = readToken;
            this.WriteToken = writeToken;

            this.Initialized = true;

            this.ConfigTypes.Clear();
            this.ConfigTypes.AddRange(configTypes);

            return this;
        }

        public virtual IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            if (this.ConfigTypes == null || !this.ConfigTypes.Contains(typeof(TConfig)))
            {
                throw new ConfigTypeNotFoundException(
                    $"Configuration type {typeof(TConfig).Name} is not accessible. " +
                    $"Available types: {string.Join(", ", this.ConfigTypes?.Select(t => t.Name) ?? [])}"
                );
            }
            return new ConfigAccessorFor<TConfig>(this.ConfigService, this.OwnerToken, this.ReadToken, this.WriteToken);
        }
    }

    public class ConfigAccessorFor<TConfig>(IConfigService service, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        : IConfigAccessorFor<TConfig> where TConfig : class
    {
        public readonly IConfigService ConfigService = service;
        public readonly Token? OwnerToken = ownerToken;
        public readonly Token? ReadToken = readToken;
        public readonly Token? WriteToken = writeToken;

        public ConfigAccessorFor(IConfigService service, ITokenSet tokenSet)
            : this(service, tokenSet.Owner, tokenSet.Read, tokenSet.Write) { }

        public virtual T Default<T>(string key)
            => this.ConfigService.GetDefault<T>(typeof(TConfig), key, this.OwnerToken, this.ReadToken)!;
        public virtual T Get<T>(string key)
            => this.ConfigService.GetSetting<T>(typeof(TConfig), key, this.OwnerToken, this.ReadToken)!;
        public virtual void Set<T>(string key, T value)
            => this.ConfigService.SetSetting(typeof(TConfig), key, value, this.OwnerToken, this.WriteToken);
        public virtual void Save()
            => this.ConfigService.SaveSettings(typeof(TConfig), this.OwnerToken, this.WriteToken);
        public virtual async Task SaveAsync(CancellationToken cancellationToken = default)
            => await this.ConfigService.SaveSettingsAsync(typeof(TConfig), this.OwnerToken, this.WriteToken, cancellationToken);

        public virtual TConfig Get()
            => (TConfig)this.ConfigService.GetConfigInstance(typeof(TConfig), this.OwnerToken, this.ReadToken);
        public virtual void Save(TConfig config)
            => this.ConfigService.SaveConfigInstance(typeof(TConfig), config, this.OwnerToken, this.WriteToken);
        public virtual async Task SaveAsync(TConfig config, CancellationToken cancellationToken = default)
            => await this.ConfigService.SaveConfigInstanceAsync(typeof(TConfig), config, this.OwnerToken, this.WriteToken, cancellationToken);
    }
}