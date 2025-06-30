using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;

namespace PlugHub.Models
{
    public readonly record struct TokenSet(Token Read, Token Write) : ITokenSet
    {
        public readonly void Deconstruct(out Token read, out Token write)
        {
            read = this.Read;
            write = this.Write;
        }
    }
}