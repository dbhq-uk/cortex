namespace Cortex.Core.Email;

/// <summary>OAuth token set containing access and refresh tokens.</summary>
public sealed record TokenSet
{
    /// <summary>The OAuth access token.</summary>
    public required string AccessToken { get; init; }

    /// <summary>The OAuth refresh token for obtaining new access tokens.</summary>
    public required string RefreshToken { get; init; }

    /// <summary>When the access token expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
