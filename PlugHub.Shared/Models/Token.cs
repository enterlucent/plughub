namespace PlugHub.Shared.Models
{
    /// <summary>
    /// Represents an immutable security token, backed by a <see cref="Guid"/>, used for access control and identification.
    /// </summary>
    public readonly struct Token : IEquatable<Token>
    {
        /// <summary>
        /// The underlying GUID value of the token.
        /// </summary>
        private readonly Guid value;


        /// <summary>
        /// Gets a public token instance (all-zero GUID), representing unrestricted or anonymous access.
        /// </summary>
        public static Token Public => new(Guid.Empty);

        /// <summary>
        /// Gets a blocked token instance (all-ones GUID), representing explicitly denied access.
        /// </summary>
        public static Token Blocked => new(new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"));


        /// <summary>
        /// Initializes a new instance of the <see cref="Token"/> struct with the specified GUID value.
        /// </summary>
        /// <param name="value">The GUID value for the token.</param>
        private Token(Guid value) => this.value = value;


        /// <summary>
        /// Creates a new <see cref="Token"/> with a randomly generated GUID.
        /// </summary>
        /// <returns>A new <see cref="Token"/> instance.</returns>
        public static Token New() => new(Guid.NewGuid());

        /// <summary>
        /// Creates a <see cref="Token"/> from an existing GUID.
        /// </summary>
        /// <param name="guid">The GUID to use for the token.</param>
        /// <returns>A <see cref="Token"/> instance representing the specified GUID.</returns>
        public static Token FromGuid(Guid guid) => new(guid);


        /// <summary>
        /// Gets a value indicating whether this token is the public (unrestricted) token.
        /// </summary>
        public bool IsPublic => this.value == Guid.Empty;

        /// <summary>
        /// Gets a value indicating whether this token is the blocked (denied) token.
        /// </summary>
        public bool IsBlocked => this.value == Blocked.value;


        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Token token && this.value.Equals(token.value);

        /// <inheritdoc/>
        public bool Equals(Token other) => this.value.Equals(other.value);

        /// <inheritdoc/>
        public override int GetHashCode() => this.value.GetHashCode();

        /// <summary>
        /// Determines whether two <see cref="Token"/> instances are equal.
        /// </summary>
        public static bool operator ==(Token left, Token right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="Token"/> instances are not equal.
        /// </summary>
        public static bool operator !=(Token left, Token right) => !left.Equals(right);

        /// <summary>
        /// Returns the string representation of the token's GUID.
        /// </summary>
        /// <returns>The string representation of the underlying GUID.</returns>
        public override string ToString() => this.value.ToString();

        /// <summary>
        /// Implicitly converts a <see cref="Token"/> to its underlying <see cref="Guid"/>.
        /// </summary>
        /// <param name="token">The token to convert.</param>
        public static implicit operator Guid(Token token) => token.value;
    }
}
