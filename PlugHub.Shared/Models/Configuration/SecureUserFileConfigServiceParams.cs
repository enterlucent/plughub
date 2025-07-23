using PlugHub.Shared.Interfaces.Models;
using System.Text.Json;

namespace PlugHub.Shared.Models.Configuration
{
    public record SecureUserFileConfigServiceParams(IEncryptionContext? EncryptionContext = null, string? UserConfigUriOverride = null, string? ConfigUriOverride = null, Token? Owner = null, Token? Read = null, Token? Write = null, JsonSerializerOptions? JsonSerializerOptions = null, bool ReloadOnChange = false)
        : UserConfigServiceParams(UserConfigUriOverride, ConfigUriOverride, Owner, Read, Write, JsonSerializerOptions, ReloadOnChange);
}