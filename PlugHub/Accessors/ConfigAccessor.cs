using PlugHub.Models;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Accessors
{
    public class ConfigAccessor(IConfigService configService, ITokenService tokenService, IList<Type> configTypes, Token ownerToken, Token readToken, Token writeToken)
        : IConfigAccessor
    {
        public readonly IConfigService ConfigService = configService;
        public readonly ITokenService TokenService = tokenService;
        public readonly IList<Type> ConfigTypes = configTypes;
        public readonly Token OwnerToken = ownerToken;
        public readonly Token ReadToken = readToken;
        public readonly Token WriteToken = writeToken;

        public ConfigAccessor(IConfigService service, ITokenService tokenService, IList<Type> configTypes, TokenSet tokenSet)
            : this(service, tokenService, configTypes, tokenSet.Owner, tokenSet.Read, tokenSet.Write) { }

        public virtual IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            if (!this.ConfigTypes.Contains(typeof(TConfig)))
            {
                throw new ConfigTypeNotFoundException(
                    $"Configuration type {typeof(TConfig).Name} is not accessible. " +
                    $"Available types: {string.Join(", ", this.ConfigTypes.Select(t => t.Name))}"
                );
            }
            return new ConfigAccessorFor<TConfig>(this.ConfigService, this.TokenService, this.OwnerToken, this.ReadToken, this.WriteToken);
        }
    }

    public class ConfigAccessorFor<TConfig>(IConfigService service, ITokenService tokenService, Token ownerToken, Token readToken, Token writeToken)
        : IConfigAccessorFor<TConfig> where TConfig : class
    {
        public readonly IConfigService ConfigService = service;
        public readonly ITokenService TokenService = tokenService;
        public readonly Token OwnerToken = ownerToken;
        public readonly Token ReadToken = readToken;
        public readonly Token WriteToken = writeToken;

        public ConfigAccessorFor(IConfigService service, ITokenService tokenService, TokenSet tokenSet)
            : this(service, tokenService, tokenSet.Owner, tokenSet.Read, tokenSet.Write) { }

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