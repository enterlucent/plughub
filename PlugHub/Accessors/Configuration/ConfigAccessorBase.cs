using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Accessors.Configuration
{
    public abstract class ConfigAccessorBase(ILogger<IConfigAccessor> logger)
        : IConfigAccessor
    {
        public Type AccessorInterface { get; init; } = typeof(object);

        protected readonly ILogger<IConfigAccessor> Logger = logger;

        protected IConfigService? ConfigService;
        protected List<Type> ConfigTypes = [];

        protected bool Initialized;

        protected Token? OwnerToken;
        protected Token? ReadToken;
        protected Token? WriteToken;

        public virtual IConfigAccessor SetConfigTypes(IList<Type> configTypes)
        {
            this.ConfigTypes = [.. configTypes];

            return this;
        }
        public virtual IConfigAccessor SetConfigService(IConfigService configService)
        {
            this.ConfigService = configService;

            return this;
        }
        public virtual IConfigAccessor SetAccess(Token ownerToken, Token readToken, Token writeToken)
        {
            this.OwnerToken = ownerToken;
            this.ReadToken = readToken;
            this.WriteToken = writeToken;

            return this;
        }
        public virtual IConfigAccessor SetAccess(ITokenSet tokenSet)
        {
            this.OwnerToken = tokenSet.Owner;
            this.ReadToken = tokenSet.Read;
            this.WriteToken = tokenSet.Write;

            return this;
        }

        public virtual IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
            => throw new NotImplementedException();
        public virtual IConfigAccessorFor<TConfig> CreateFor<TConfig>(IConfigService configService, Token ownerToken, Token readToken, Token writeToken) where TConfig : class
            => throw new NotImplementedException();
        public virtual IConfigAccessorFor<TConfig> CreateFor<TConfig>(IConfigService configService, ITokenSet tokenSet) where TConfig : class
            => this.CreateFor<TConfig>(configService, tokenSet.Owner, tokenSet.Read, tokenSet.Write);
    }

    public abstract class ConfigAccessorForBase<TConfig>(IConfigService configService, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        : IConfigAccessorFor<TConfig>, IFileConfigAccessorFor<TConfig> where TConfig : class
    {
        public readonly IConfigService ConfigService = configService;
        public Token? OwnerToken = ownerToken;
        public Token? ReadToken = readToken;
        public Token? WriteToken = writeToken;

        public ConfigAccessorForBase(IConfigService service, ITokenSet tokenSet)
            : this(service, tokenSet.Owner, tokenSet.Read, tokenSet.Write) { }

        public IConfigAccessorFor<TConfig> SetAccess(Token ownerToken, Token readToken, Token writeToken)
        {
            this.OwnerToken = ownerToken;
            this.ReadToken = readToken;
            this.WriteToken = writeToken;

            return this;
        }
        public IConfigAccessorFor<TConfig> SetAccess(ITokenSet tokenSet)
        {
            this.OwnerToken = tokenSet.Owner;
            this.ReadToken = tokenSet.Read;
            this.WriteToken = tokenSet.Write;

            return this;
        }

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