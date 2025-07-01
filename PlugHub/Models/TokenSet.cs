using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;

namespace PlugHub.Models
{
    public readonly record struct TokenSet(Token Owner, Token Read, Token Write) : ITokenSet
    {
        public readonly void Deconstruct(out Token ownerToken, out Token readToken, out Token writeToken)
        {
            ownerToken = this.Owner;
            readToken = this.Read;
            writeToken = this.Write;
        }
    }
}