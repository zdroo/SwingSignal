namespace RegimeDeck.Domain.Entities;

/// One row per refresh token. A login opens a "family" (a rotation chain, i.e.
/// one device/session); every refresh rotates the current token into a new one
/// in the same family and revokes the old. Only the SHA-256 hash of the raw
/// token is stored, so a database leak never yields a usable session.
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// SHA-256 (hex) of the raw token the client holds — never the raw value.
    public string TokenHash { get; set; } = string.Empty;

    /// Shared across a rotation chain. Reuse of a revoked token revokes the
    /// whole family (theft detection).
    public Guid FamilyId { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// Set when the token is rotated away, revoked on logout, or revoked as part
    /// of a family after reuse detection.
    public DateTime? RevokedAt { get; set; }

    /// The token this one was rotated into — a breadcrumb for auditing chains.
    public Guid? ReplacedByTokenId { get; set; }

    /// Best-effort device label (User-Agent) so a future "your sessions" screen
    /// can name them. Never trusted for anything security-relevant.
    public string? UserAgent { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
