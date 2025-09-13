using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;


namespace PlugHub.Accessors.Configuration
{
    public class FileConfigAccessor : BaseConfigAccessor, IConfigAccessor, IFileConfigAccessor
    {
        public FileConfigAccessor(ILogger<IConfigAccessor> logger, ITokenService tokenService)
            : base(logger, tokenService)
        {
            this.AccessorInterface = typeof(IFileConfigAccessor);
        }

        #region FileConfigAccessor: Fluent Configuration API

        public override IFileConfigAccessor SetConfigTypes(IList<Type> configTypes)
        {
            base.SetConfigTypes(configTypes);

            return this;
        }
        public override IFileConfigAccessor SetConfigService(IConfigService configService)
        {
            base.SetConfigService(configService);

            return this;
        }

        public override IFileConfigAccessor SetAccess(Token ownerToken, Token readToken, Token writeToken)
        {
            base.SetAccess(ownerToken, readToken, writeToken);

            return this;
        }
        public override IFileConfigAccessor SetAccess(ITokenSet tokenSet)
        {
            base.SetAccess(tokenSet);

            return this;
        }

        #endregion

        #region FileConfigAccessor: Factory Methods

        public override IFileConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            if (this.ConfigService == null)
                throw new InvalidOperationException("ConfigService must be set before creating typed accessors");

            if (this.ConfigTypes == null)
                throw new InvalidOperationException("ConfigTypes must be set before creating typed accessors");

            if (this.ConfigTypes.Contains(typeof(TConfig)))
                return new FileConfigAccessorFor<TConfig>(this.TokenService, this.ConfigService, this.OwnerToken, this.ReadToken, this.WriteToken);

            string availableTypes = this.ConfigTypes.Count > 0
                ? string.Join(", ", this.ConfigTypes.Select(t => t.Name))
                : "none configured";

            throw new InvalidOperationException($"Configuration type {typeof(TConfig).Name} is not accessible. Available types: {availableTypes}");
        }

        public override IFileConfigAccessorFor<TConfig> CreateFor<TConfig>(ITokenService tokenService, IConfigService configService, Token? ownerToken, Token? readToken, Token? writeToken) where TConfig : class
            => new FileConfigAccessorFor<TConfig>(tokenService, configService, ownerToken, readToken, writeToken);
        public override IFileConfigAccessorFor<TConfig> CreateFor<TConfig>(ITokenService tokenService, IConfigService configService, ITokenSet tokenSet) where TConfig : class
            => this.CreateFor<TConfig>(tokenService, configService, tokenSet.Owner, tokenSet.Read, tokenSet.Write);

        #endregion
    }

    public class FileConfigAccessorFor<TConfig>(ITokenService tokenService, IConfigService configService, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        : BaseConfigAccessorFor<TConfig>(tokenService, configService, ownerToken, readToken, writeToken), IConfigAccessorFor<TConfig>, IFileConfigAccessorFor<TConfig> where TConfig : class
    {
        public FileConfigAccessorFor(ITokenService tokenService, IConfigService configService, ITokenSet tokenSet)
            : this(tokenService, configService, tokenSet.Owner, tokenSet.Read, tokenSet.Write) { }
    }
}