using System;

namespace BirthdayReminder.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsRevoked => RevokedAtUtc != null;
}
