using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Accessors.Configuration
{
    public class FileConfigAccessor
        : ConfigAccessorBase, IConfigAccessor, IFileConfigAccessor
    {
        public FileConfigAccessor(ILogger<IConfigAccessor> logger)
            : base(logger)
        {
            this.AccessorInterface = typeof(IFileConfigAccessor);
        }

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

        public override IFileConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            //TODO: Give proper message
            if (this.ConfigService == null)
                throw new InvalidOperationException("");

            if (this.ConfigTypes == null || !this.ConfigTypes.Contains(typeof(TConfig)))
            {
                throw new KeyNotFoundException(
                    $"Configuration type {typeof(TConfig).Name} is not accessible. " +
                    $"Available types: {string.Join(", ", this.ConfigTypes?.Select(t => t.Name) ?? [])}"
                );
            }
            return new FileConfigAccessorFor<TConfig>(this.ConfigService, this.OwnerToken, this.ReadToken, this.WriteToken);
        }
        public override IFileConfigAccessorFor<TConfig> CreateFor<TConfig>(IConfigService configService, Token ownerToken, Token readToken, Token writeToken) where TConfig : class
            => new FileConfigAccessorFor<TConfig>(configService, ownerToken, readToken, writeToken);
        public override IFileConfigAccessorFor<TConfig> CreateFor<TConfig>(IConfigService configService, ITokenSet tokenSet) where TConfig : class
            => this.CreateFor<TConfig>(configService, tokenSet.Owner, tokenSet.Read, tokenSet.Write);
    }

    public class FileConfigAccessorFor<TConfig>(IConfigService service, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        : ConfigAccessorForBase<TConfig>(service, ownerToken, readToken, writeToken), IConfigAccessorFor<TConfig>, IFileConfigAccessorFor<TConfig> where TConfig : class
    {
        public FileConfigAccessorFor(IConfigService service, ITokenSet tokenSet)
            : this(service, tokenSet.Owner, tokenSet.Read, tokenSet.Write) { }
    }
}