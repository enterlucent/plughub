using Microsoft.Extensions.Logging;
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

namespace PlugHub.Accessors
{
    public class SecureConfigAccessor(ILogger<ISecureConfigAccessor> logger, IConfigService configService) 
        : ConfigAccessor((ILogger<IConfigAccessor>)logger, configService), ISecureConfigAccessor
    {
        protected new readonly ILogger<ISecureConfigAccessor> Logger = logger;
        public IEncryptionContext? EncryptionContext;

        public ISecureConfigAccessor Init(IList<Type> configTypes, IEncryptionContext encryptionContext, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        {
            if (this.Initialized)
                throw new InvalidOperationException("Accessor already initialised");

            base.Init(configTypes, ownerToken, readToken, writeToken);

            this.EncryptionContext = encryptionContext;

            return this;
        }

        public new ISecureConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class
        {
            if (!this.ConfigTypes.Contains(typeof(TConfig)))
                throw new TypeAccessException(
                    $"Configuration type {typeof(TConfig).Name} is not accessible. " +
                    $"Registered types: {string.Join(", ", this.ConfigTypes.Select(t => t.Name))}"
                );

            if (this.EncryptionContext is null)
                throw new ArgumentException(
                    "EncryptionContext is null. Call Init(...) with a valid IEncryptionContext before accessing secure configuration.");

            return new SecureConfigAccessorFor<TConfig>(this.ConfigService, this.EncryptionContext, this.OwnerToken, this.ReadToken, this.WriteToken);
        }

        private class SecureConfigAccessorFor<TConfig>(IConfigService configService, IEncryptionContext encryptionContext, Token? ownerToken, Token? readToken, Token? writeToken)
            : ConfigAccessorFor<TConfig>(configService, ownerToken, readToken, writeToken), ISecureConfigAccessorFor<TConfig> where TConfig : class
        {
            public readonly IEncryptionContext EncryptionContext = encryptionContext
                ?? throw new ArgumentNullException(nameof(encryptionContext));

            private static readonly ConcurrentDictionary<Type, PropertyInfo[]> propertyCache = new();


            public override T Get<T>(string key)
            {
                SecureValue? secureValue = this.ConfigService.GetSetting<SecureValue>(typeof(TConfig), key, this.OwnerToken, this.ReadToken);

                if (secureValue is null)
                    return base.Get<T>(key);
                else
                    return secureValue.As<T>(this.EncryptionContext);
            }
            public override void Set<T>(string key, T value)
            {
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
                TConfig instance = base.Get();

                foreach (PropertyInfo prop in GetCachedProperties())
                {
                    if (!prop.CanWrite) continue;

                    bool isSecure = prop.PropertyType == typeof(SecureValue) || Attribute.IsDefined(prop, typeof(SecureAttribute));

                    if (!isSecure) continue;

                    MethodInfo genericGet = typeof(SecureConfigAccessorFor<TConfig>)
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
                Task.Run(async () =>
                {
                    try
                    {
                        await this.SaveAsync(instance);

                        this.ConfigService.OnSaveOperationComplete(instance.GetType());
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
                ArgumentNullException.ThrowIfNull(instance);

                cancellationToken.ThrowIfCancellationRequested();

                foreach (PropertyInfo prop in GetCachedProperties())
                {
                    object? raw = prop.GetValue(instance);

                    MethodInfo setGeneric = typeof(SecureConfigAccessorFor<TConfig>)
                        .GetMethod(nameof(Set))!
                        .MakeGenericMethod(prop.PropertyType);

                    setGeneric.Invoke(this, [prop.Name, raw]);
                }

                cancellationToken.ThrowIfCancellationRequested();

                await this.ConfigService.SaveSettingsAsync(typeof(TConfig), this.OwnerToken, this.WriteToken, cancellationToken);
            }


            private static PropertyInfo[] GetCachedProperties()
                => propertyCache.GetOrAdd(
                       typeof(TConfig),
                       t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
            private static PropertyInfo? GetCachedProperty(string propertyName)
            {
                var props = propertyCache.GetOrAdd(
                    typeof(TConfig),
                    t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                return Array.Find(props, p => p.Name == propertyName);
            }
        }
    }
}