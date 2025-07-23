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

namespace PlugHub.Accessors.Configuration
{
    public abstract class ConfigAccessorBase(ILogger<IConfigAccessor> logger, ITokenService tokenService) : IConfigAccessor
    {
        public Type AccessorInterface { get; init; } = typeof(object);

        protected readonly ILogger<IConfigAccessor> Logger = logger;
        protected readonly ITokenService TokenService = tokenService;

        protected IConfigService? ConfigService;
        protected List<Type> ConfigTypes = [];
        protected bool Initialized;

        protected Token? OwnerToken;
        protected Token? ReadToken;
        protected Token? WriteToken;

        #region ConfigAccessorBase: Fluent Configuration API

        public virtual IConfigAccessor SetConfigTypes(IList<Type> configTypes)
        {
            ArgumentNullException.ThrowIfNull(configTypes);

            this.ConfigTypes = [.. configTypes];
            return this;
        }
        public virtual IConfigAccessor SetConfigService(IConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(configService);

            this.ConfigService = configService;
            return this;
        }
        public virtual IConfigAccessor SetAccess(Token ownerToken, Token readToken, Token writeToken)
        {
            ITokenSet tokenSet = this.TokenService.CreateTokenSet(ownerToken, readToken, writeToken);

            this.OwnerToken = tokenSet.Owner;
            this.ReadToken = tokenSet.Read;
            this.WriteToken = tokenSet.Write;

            return this;
        }
        public virtual IConfigAccessor SetAccess(ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.SetAccess(tokenSet.Owner, tokenSet.Read, tokenSet.Write);
        }

        #endregion

        #region ConfigAccessorBase: Factory Methods

        public virtual IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            if (this.ConfigService == null)
            {
                throw new InvalidOperationException("ConfigService must be set before creating typed accessors");
            }

            if (this.ConfigTypes == null)
            {
                throw new InvalidOperationException("ConfigTypes must be set before creating typed accessors");
            }

            bool isRegisteredType = this.ConfigTypes.Contains(typeof(TConfig));

            if (!isRegisteredType)
            {
                string availableTypes = this.ConfigTypes.Count > 0
                    ? string.Join(", ", this.ConfigTypes.Select(t => t.Name))
                    : "none configured";

                throw new InvalidOperationException($"Configuration type {typeof(TConfig).Name} is not accessible. Available types: {availableTypes}");
            }

            return this.CreateFor<TConfig>(this.TokenService, this.ConfigService, this.OwnerToken, this.ReadToken, this.WriteToken);
        }

        public virtual IConfigAccessorFor<TConfig> CreateFor<TConfig>(ITokenService tokenService, IConfigService configService, Token? ownerToken, Token? readToken, Token? writeToken) where TConfig : class
            => throw new NotImplementedException();
        public virtual IConfigAccessorFor<TConfig> CreateFor<TConfig>(ITokenService tokenService, IConfigService configService, ITokenSet tokenSet) where TConfig : class
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(tokenService);
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.CreateFor<TConfig>(tokenService, configService, tokenSet.Owner, tokenSet.Read, tokenSet.Write);
        }

        #endregion
    }

    public abstract class ConfigAccessorForBase<TConfig>(ITokenService tokenService, IConfigService configService, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        : IConfigAccessorFor<TConfig>, IFileConfigAccessorFor<TConfig> where TConfig : class
    {
        public readonly IConfigService ConfigService = configService;
        protected readonly ITokenService TokenService = tokenService;

        public Token? OwnerToken = ownerToken;
        public Token? ReadToken = readToken;
        public Token? WriteToken = writeToken;

        public ConfigAccessorForBase(ITokenService tokenService, IConfigService service, ITokenSet tokenSet)
            : this(tokenService, service, tokenSet?.Owner, tokenSet?.Read, tokenSet?.Write)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(tokenService);
            ArgumentNullException.ThrowIfNull(tokenSet);
        }

        #region ConfigAccessorForBase: Access Configuration

        public IConfigAccessorFor<TConfig> SetAccess(Token ownerToken, Token readToken, Token writeToken)
        {
            ITokenSet tokenSet = this.TokenService.CreateTokenSet(ownerToken, readToken, writeToken);

            this.OwnerToken = tokenSet.Owner;
            this.ReadToken = tokenSet.Read;
            this.WriteToken = tokenSet.Write;

            return this;
        }
        public IConfigAccessorFor<TConfig> SetAccess(ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.SetAccess(tokenSet.Owner, tokenSet.Read, tokenSet.Write);
        }

        #endregion

        #region ConfigAccessorForBase: Property Access

        public virtual T Default<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return this.ConfigService.GetDefault<T>(typeof(TConfig), key, this.OwnerToken, this.ReadToken)!;
        }
        public virtual T Get<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return this.ConfigService.GetSetting<T>(typeof(TConfig), key, this.OwnerToken, this.ReadToken)!;
        }
        public virtual void Set<T>(string key, T value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            this.ConfigService.SetSetting(typeof(TConfig), key, value, this.OwnerToken, this.WriteToken);
        }
        public virtual void Save()
            => this.ConfigService.SaveSettings(typeof(TConfig), this.OwnerToken, this.WriteToken);
        public virtual async Task SaveAsync(CancellationToken cancellationToken = default)
            => await this.ConfigService.SaveSettingsAsync(typeof(TConfig), this.OwnerToken, this.WriteToken, cancellationToken);

        #endregion

        #region ConfigAccessorForBase: Instance Operations

        public virtual TConfig Get()
            => (TConfig)this.ConfigService.GetConfigInstance(typeof(TConfig), this.OwnerToken, this.ReadToken);
        public virtual void Save(TConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            this.ConfigService.SaveConfigInstance(typeof(TConfig), config, this.OwnerToken, this.WriteToken);
        }
        public virtual async Task SaveAsync(TConfig config, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(config);

            await this.ConfigService.SaveConfigInstanceAsync(typeof(TConfig), config, this.OwnerToken, this.WriteToken, cancellationToken);
        }

        #endregion
    }
}
