using Microsoft.Extensions.Logging;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;


namespace PlugHub.Accessors.Configuration
{
    public class SecureFileConfigAccessor
        : FileConfigAccessor, IConfigAccessor, IFileConfigAccessor, ISecureFileConfigAccessor
    {
        protected IEncryptionService EncryptionService;
        protected IEncryptionContext? EncryptionContext;

        public SecureFileConfigAccessor(ILogger<IConfigAccessor> logger, IEncryptionService encryptionService)
            : base(logger)
        {
            this.EncryptionService = encryptionService;
            this.AccessorInterface = typeof(ISecureFileConfigAccessor);
        }

        public virtual ISecureFileConfigAccessor SetEncryptionService(IEncryptionService encryptionService)
        {
            this.EncryptionService = encryptionService;

            return this;
        }
        public virtual ISecureFileConfigAccessor SetEncryptionContext(IEncryptionContext encryptionContext)
        {
            this.EncryptionContext = encryptionContext;

            return this;
        }

        public override ISecureFileConfigAccessor SetConfigTypes(IList<Type> configTypes)
        {
            base.SetConfigTypes(configTypes);

            return this;
        }
        public override ISecureFileConfigAccessor SetConfigService(IConfigService configService)
        {
            base.SetConfigService(configService);

            return this;
        }
        public override ISecureFileConfigAccessor SetAccess(Token ownerToken, Token readToken, Token writeToken)
        {
            base.SetAccess(ownerToken, readToken, writeToken);

            return this;
        }
        public override ISecureFileConfigAccessor SetAccess(ITokenSet tokenSet)
        {
            base.SetAccess(tokenSet);

            return this;
        }

        public override ISecureFileConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            //TODO: Give proper message
            if (this.ConfigService == null)
                throw new InvalidOperationException("");

            if (!this.ConfigTypes.Contains(typeof(TConfig)))
                throw new TypeAccessException(
                    $"Configuration type {typeof(TConfig).Name} is not accessible. " +
                    $"Registered types: {string.Join(", ", this.ConfigTypes.Select(t => t.Name))}"
                );

            if (this.EncryptionContext is null)
                throw new ArgumentException("EncryptionContext is null. Call Init(...) with a valid IEncryptionContext before accessing secure configuration.");

            return new SecureFileConfigAccessorFor<TConfig>(this.ConfigService, this.EncryptionContext, this.OwnerToken, this.ReadToken, this.WriteToken);
        }
        public override ISecureFileConfigAccessorFor<TConfig> CreateFor<TConfig>(IConfigService configService, Token ownerToken, Token readToken, Token writeToken) where TConfig : class
        {
            IEncryptionContext encryptionContext =
                this.EncryptionService.GetEncryptionContext(typeof(TConfig), typeof(TConfig).ToDeterministicGuid());

            return new SecureFileConfigAccessorFor<TConfig>(configService, encryptionContext, ownerToken, readToken, writeToken);
        }
        public override ISecureFileConfigAccessorFor<TConfig> CreateFor<TConfig>(IConfigService configService, ITokenSet tokenSet) where TConfig : class
            => this.CreateFor<TConfig>(configService, tokenSet.Owner, tokenSet.Read, tokenSet.Write);

        public class SecureFileConfigAccessorFor<TConfig>(IConfigService configService, IEncryptionContext encryptionContext, Token? ownerToken, Token? readToken, Token? writeToken)
            : FileConfigAccessorFor<TConfig>(configService, ownerToken, readToken, writeToken), IFileConfigAccessorFor<TConfig>, ISecureFileConfigAccessorFor<TConfig> where TConfig : class
        {
            protected IEncryptionContext EncryptionContext = encryptionContext
                ?? throw new ArgumentNullException(nameof(encryptionContext));

            protected static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

            public ISecureFileConfigAccessorFor<TConfig> SetEncryptionContext(IEncryptionContext encryptionContext)
            {
                this.EncryptionContext = encryptionContext;

                return this;
            }

            public override T Get<T>(string key)
            {
                ValidateEncryptionContext(this.EncryptionContext);

                SecureValue? secureValue = this.ConfigService.GetSetting<SecureValue>(typeof(TConfig), key, this.OwnerToken, this.ReadToken);

                if (secureValue is null)
                    return base.Get<T>(key);
                else
                    return secureValue.As<T>(this.EncryptionContext);
            }
            public override void Set<T>(string key, T value)
            {
                ValidateEncryptionContext(this.EncryptionContext);

                PropertyInfo prop = GetCachedProperty(key)
                    ?? throw new KeyNotFoundException($"Property {key} not found.");

                bool isSecure = prop.PropertyType == typeof(SecureValue) || Attribute.IsDefined(prop, typeof(SecureAttribute));

                if (!isSecure)
                {
                    base.Set(key, value);

                    return;
                }

                SecureValue? userBlob =
                    this.ConfigService.GetSetting<SecureValue>(typeof(TConfig), key, this.OwnerToken, this.ReadToken);

                if (userBlob != null && EqualityComparer<T>.Default.Equals(userBlob.As<T>(this.EncryptionContext), value))
                    return;

                SecureValue? defaultBlob =
                    this.ConfigService.GetDefault<SecureValue>(typeof(TConfig), key, this.OwnerToken, this.ReadToken);

                if (defaultBlob != null && EqualityComparer<T>.Default.Equals(defaultBlob.As<T>(this.EncryptionContext), value))
                {
                    base.Set<SecureValue>(key, null!);
                    return;
                }

                SecureValue wrapped = SecureValue.From(value!, this.EncryptionContext);

                base.Set(key, wrapped);
            }

            public override TConfig Get()
            {
                ValidateEncryptionContext(this.EncryptionContext);

                TConfig instance = base.Get();

                foreach (PropertyInfo prop in GetCachedProperties())
                {
                    if (!prop.CanWrite) continue;

                    bool isSecure = prop.PropertyType == typeof(SecureValue) || Attribute.IsDefined(prop, typeof(SecureAttribute));

                    if (!isSecure) continue;

                    MethodInfo genericGet = typeof(SecureFileConfigAccessorFor<TConfig>)
                       .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Single(m =>
                               m.Name == nameof(Get) &&
                               m.IsGenericMethodDefinition &&
                               m.GetParameters().Length == 1 &&
                               m.GetParameters()[0].ParameterType == typeof(string));

                    MethodInfo closed = genericGet.MakeGenericMethod(prop.PropertyType);

                    object? plain = closed.Invoke(this, [prop.Name]);
                    if (plain is null) continue;

                    if (prop.PropertyType.IsInstanceOfType(plain))
                        prop.SetValue(instance, plain);
                    else if (plain is IConvertible)
                        prop.SetValue(instance, Convert.ChangeType(plain, prop.PropertyType));
                }

                return instance;
            }
            public override void Save(TConfig instance)
            {
                ValidateEncryptionContext(this.EncryptionContext);

                Task.Run(async () =>
                {
                    try
                    {
                        await this.SaveAsync(instance);

                        this.ConfigService.OnSaveOperationComplete(this, instance.GetType());
                    }
                    catch
                    {
                        //TODO: Sort this out
                        //this.ConfigService.OnSaveOperationError(ex, ConfigSaveOperation.SaveConfigInstance, configType);
                    }
                });
            }
            public override async Task SaveAsync(TConfig instance, CancellationToken cancellationToken = default)
            {
                ValidateEncryptionContext(this.EncryptionContext);

                ArgumentNullException.ThrowIfNull(instance);

                cancellationToken.ThrowIfCancellationRequested();

                foreach (PropertyInfo prop in GetCachedProperties())
                {
                    object? raw = prop.GetValue(instance);

                    MethodInfo setGeneric = typeof(SecureFileConfigAccessorFor<TConfig>)
                        .GetMethod(nameof(Set))!
                        .MakeGenericMethod(prop.PropertyType);

                    setGeneric.Invoke(this, [prop.Name, raw]);
                }

                cancellationToken.ThrowIfCancellationRequested();

                await this.ConfigService.SaveSettingsAsync(typeof(TConfig), this.OwnerToken, this.WriteToken, cancellationToken);
            }


            protected static void ValidateEncryptionContext(IEncryptionContext? encryptionContext)
            {
                if (encryptionContext is null)
                    throw new ArgumentException(
                        "EncryptionContext is null. Call Init(...) with a valid IEncryptionContext before accessing secure configuration.");
            }
            protected static PropertyInfo[] GetCachedProperties()
                => PropertyCache.GetOrAdd(
                       typeof(TConfig),
                       t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
            protected static PropertyInfo? GetCachedProperty(string propertyName)
            {
                PropertyInfo[] props = PropertyCache.GetOrAdd(
                    typeof(TConfig),
                    t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                return Array.Find(props, p => p.Name == propertyName);
            }
        }
    }
}