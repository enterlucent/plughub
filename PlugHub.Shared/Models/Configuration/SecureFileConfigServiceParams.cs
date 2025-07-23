using PlugHub.Shared.Interfaces.Models;
using System.Text.Json;

namespace PlugHub.Shared.Models.Configuration
{
    public record SecureFileConfigServiceParams(IEncryptionContext? EncryptionContext = null, string? ConfigUriOverride = null, Token? Owner = null, Token? Read = null, Token? Write = null, JsonSerializerOptions? JsonSerializerOptions = null, bool ReloadOnChange = false)
        : FileConfigServiceParams(ConfigUriOverride, Owner, Read, Write, JsonSerializerOptions, ReloadOnChange);
}