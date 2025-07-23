using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;
using System;


namespace PlugHub.Models
{
    public readonly record struct TokenSet(Token Owner, Token Read, Token Write) : ITokenSet
    {
        #region TokenSet: Core API

        public readonly void Deconstruct(out Token ownerToken, out Token readToken, out Token writeToken)
        {
            ownerToken = this.Owner;
            readToken = this.Read;
            writeToken = this.Write;
        }

        #endregion

        #region TokenSet: Common Factory Methods

        public static TokenSet CreateSecure(Token owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            return new TokenSet(owner, owner, owner);
        }
        public static TokenSet CreateReadOnly(Token owner, Token reader)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(reader);

            return new TokenSet(owner, reader, Token.Blocked);
        }
        public static TokenSet CreatePublicReadOnly(Token owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            return new TokenSet(owner, Token.Public, Token.Blocked);
        }

        #endregion

        #region TokenSet: Immutable Updates

        public TokenSet WithOwner(Token newOwner)
        {
            ArgumentNullException.ThrowIfNull(newOwner);

            return new TokenSet(newOwner, this.Read, this.Write);
        }
        public TokenSet WithRead(Token newRead)
        {
            ArgumentNullException.ThrowIfNull(newRead);

            return new TokenSet(this.Owner, newRead, this.Write);
        }
        public TokenSet WithWrite(Token newWrite)
        {
            ArgumentNullException.ThrowIfNull(newWrite);

            return new TokenSet(this.Owner, this.Read, newWrite);
        }

        #endregion

        #region TokenSet: Deep Copy

        public TokenSet DeepCopy()
        {
            return new TokenSet(this.Owner, this.Read, this.Write);
        }

        #endregion
    }
}