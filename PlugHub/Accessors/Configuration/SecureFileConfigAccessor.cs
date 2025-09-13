using Microsoft.Extensions.Logging;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
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

        public SecureFileConfigAccessor(ILogger<IConfigAccessor> logger, ITokenService tokenService, IEncryptionService encryptionService)
            : base(logger, tokenService)
        {
            this.EncryptionService = encryptionService;
            this.AccessorInterface = typeof(ISecureFileConfigAccessor);
        }

        #region SecureFileConfigAccessor: Fluent Configuration API

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

        #endregion

        #region SecureFileConfigAccessor: Factory Methods

        public override ISecureFileConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            if (this.ConfigService == null)
                throw new InvalidOperationException("ConfigService must be set before creating typed accessors");

            if (this.ConfigTypes == null)
                throw new InvalidOperationException("ConfigTypes must be set before creating typed accessors");

            if (!this.ConfigTypes.Contains(typeof(TConfig)))
            {
                string availableTypes = this.ConfigTypes.Count > 0
                    ? string.Join(", ", this.ConfigTypes.Select(t => t.Name))
                    : "none configured";

                throw new InvalidOperationException($"Configuration type {typeof(TConfig).Name} is not accessible. Available types: {availableTypes}");
            }

            if (this.EncryptionContext == null)
                throw new InvalidOperationException("EncryptionContext must be set before accessing secure configuration. Call SetEncryptionContext() with a valid IEncryptionContext.");

            return new SecureFileConfigAccessorFor<TConfig>(this.TokenService, this.ConfigService, this.EncryptionContext, this.OwnerToken, this.ReadToken, this.WriteToken);
        }

        public override ISecureFileConfigAccessorFor<TConfig> CreateFor<TConfig>(ITokenService tokenService, IConfigService configService, Token? ownerToken, Token? readToken, Token? writeToken) where TConfig : class
        {
            IEncryptionContext encryptionContext = this.EncryptionService.GetEncryptionContext(typeof(TConfig), typeof(TConfig).ToDeterministicGuid());

            return new SecureFileConfigAccessorFor<TConfig>(tokenService, configService, encryptionContext, ownerToken, readToken, writeToken);
        }

        public override ISecureFileConfigAccessorFor<TConfig> CreateFor<TConfig>(ITokenService tokenService, IConfigService configService, ITokenSet tokenSet) where TConfig : class
            => this.CreateFor<TConfig>(tokenService, configService, tokenSet.Owner, tokenSet.Read, tokenSet.Write);

        #endregion

        public class SecureFileConfigAccessorFor<TConfig>(ITokenService tokenService, IConfigService configService, IEncryptionContext encryptionContext, Token? ownerToken, Token? readToken, Token? writeToken)
            : FileConfigAccessorFor<TConfig>(tokenService, configService, ownerToken, readToken, writeToken), IFileConfigAccessorFor<TConfig>, ISecureFileConfigAccessorFor<TConfig> where TConfig : class
        {
            protected IEncryptionContext EncryptionContext = encryptionContext
                ?? throw new ArgumentNullException(nameof(encryptionContext));

            protected static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

            #region SecureFileConfigAccessorFor: Access Configuration

            public ISecureFileConfigAccessorFor<TConfig> SetEncryptionContext(IEncryptionContext encryptionContext)
            {
                this.EncryptionContext = encryptionContext;

                return this;
            }

            #endregion

            #region SecureFileConfigAccessorFor: Property Access

            public override T Get<T>(string key)
            {
                ValidateEncryptionContext(this.EncryptionContext);

                SecureValue? secureValue = this.ConfigService.GetValue<SecureValue>(typeof(TConfig), key, this.OwnerToken, this.ReadToken);

                if (secureValue != null)
                    return secureValue.As<T>(this.EncryptionContext);

                return base.Get<T>(key);
            }

            public override void Set<T>(string key, T value)
            {
                ValidateEncryptionContext(this.EncryptionContext);

                PropertyInfo[] properties = GetCachedProperties();
                PropertyInfo? property = Array.Find(properties, p => p.Name == key);

                bool isSecureProperty = IsSecureProperty(property);

                if (!isSecureProperty)
                {
                    base.Set(key, value);

                    return;
                }

                bool shouldUpdateValue = this.ShouldUpdateSecureValue(key, value);

                if (!shouldUpdateValue)
                    return;

                SecureValue wrappedValue = SecureValue.From(value!, this.EncryptionContext);

                base.Set(key, wrappedValue);
            }

            #endregion

            #region SecureFileConfigAccessorFor: Instance Operations

            public override TConfig Get()
            {
                ValidateEncryptionContext(this.EncryptionContext);

                TConfig instance = base.Get();

                PropertyInfo[] secureProperties = SecureFileConfigAccessorFor<TConfig>.GetSecureProperties();

                foreach (PropertyInfo property in secureProperties)
                    this.SetSecurePropertyValue(instance, property);

                return instance;
            }

            public override void Save(TConfig instance)
            {
                ValidateEncryptionContext(this.EncryptionContext);
                ArgumentNullException.ThrowIfNull(instance);

                this.SaveSecurePropertiesToConfig(instance);

                this.ConfigService.SaveValues(typeof(TConfig), this.OwnerToken, this.WriteToken);
                this.ConfigService.OnSaveOperationComplete(this, instance.GetType());
            }

            public override async Task SaveAsync(TConfig instance, CancellationToken cancellationToken = default)
            {
                ValidateEncryptionContext(this.EncryptionContext);
                ArgumentNullException.ThrowIfNull(instance);

                cancellationToken.ThrowIfCancellationRequested();

                this.SaveSecurePropertiesToConfig(instance);

                cancellationToken.ThrowIfCancellationRequested();

                await this.ConfigService.SaveValuesAsync(typeof(TConfig), this.OwnerToken, this.WriteToken, cancellationToken);
            }

            #endregion

            #region SecureFileConfigAccessorFor: Core Helper Methods

            private bool ShouldUpdateSecureValue<T>(string key, T value)
            {
                SecureValue? currentValue = this.ConfigService.GetValue<SecureValue>(
                    typeof(TConfig), key, this.OwnerToken, this.ReadToken);

                if (currentValue == null)
                    return true;

                T decryptedCurrent = currentValue.As<T>(this.EncryptionContext);

                return !EqualityComparer<T>.Default.Equals(decryptedCurrent, value);
            }

            private static PropertyInfo[] GetSecureProperties()
            {
                PropertyInfo[] allProperties = GetCachedProperties();

                return [.. allProperties.Where(prop => prop.CanWrite && IsSecureProperty(prop))];
            }

            private void SetSecurePropertyValue(TConfig instance, PropertyInfo property)
            {
                MethodInfo genericGet = typeof(SecureFileConfigAccessorFor<TConfig>)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(m =>
                        m.Name == nameof(Get) &&
                        m.IsGenericMethodDefinition &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string));

                MethodInfo typedGet = genericGet.MakeGenericMethod(property.PropertyType);

                object? decryptedValue = typedGet.Invoke(this, [property.Name]);

                if (decryptedValue == null)
                    return;

                bool isCorrectType = property.PropertyType.IsInstanceOfType(decryptedValue);
                bool isConvertible = decryptedValue is IConvertible;

                if (isCorrectType)
                {
                    property.SetValue(instance, decryptedValue);
                }
                else if (isConvertible)
                {
                    object convertedValue = Convert.ChangeType(decryptedValue, property.PropertyType);

                    property.SetValue(instance, convertedValue);
                }
            }
            private void SaveSecurePropertiesToConfig(TConfig instance)
            {
                PropertyInfo[] allProperties = GetCachedProperties();

                foreach (PropertyInfo property in allProperties)
                {
                    object? rawValue = property.GetValue(instance);

                    MethodInfo setMethod = typeof(SecureFileConfigAccessorFor<TConfig>).GetMethod(nameof(Set))!;
                    MethodInfo setGeneric = setMethod.MakeGenericMethod(property.PropertyType);

                    setGeneric.Invoke(this, [property.Name, rawValue]);
                }
            }

            #endregion

            #region SecureFileConfigAccessorFor: Static Helper Methods

            protected static void ValidateEncryptionContext(IEncryptionContext? encryptionContext)
            {
                if (encryptionContext == null)
                    throw new InvalidOperationException("EncryptionContext is null. Call SetEncryptionContext() with a valid IEncryptionContext before accessing secure configuration.");
            }
            protected static PropertyInfo[] GetCachedProperties()
                => PropertyCache.GetOrAdd(typeof(TConfig), t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
            protected static bool IsSecureProperty(PropertyInfo? property)
            {
                if (property == null)
                    return false;

                bool isSecureValueType = property.PropertyType == typeof(SecureValue);
                bool hasSecureAttribute = Attribute.IsDefined(property, typeof(SecureAttribute));

                return isSecureValueType || hasSecureAttribute;
            }

            #endregion
        }
    }
}
