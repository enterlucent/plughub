using PlugHub.Shared.Interfaces.Services;
using System.Text.Json;

namespace PlugHub.Shared.Models.Configuration
{
    public record FileConfigServiceParams(string? ConfigUriOverride = null, Token? Owner = null, Token? Read = null, Token? Write = null, JsonSerializerOptions? JsonSerializerOptions = null, bool ReloadOnChange = false)
        : IConfigServiceParams
    {
    }
}