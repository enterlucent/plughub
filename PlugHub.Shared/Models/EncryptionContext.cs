using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;

namespace PlugHub.Shared.Models
{
    public sealed class EncryptionContext(IEncryptionService encryptionService, byte[]? key = null) : IEncryptionContext
    {
        public IEncryptionService EncryptionService { get; } = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));

        public byte[] Key { get; } = key ?? [];
    }
}
