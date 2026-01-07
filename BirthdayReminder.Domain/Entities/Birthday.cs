using System;

namespace BirthdayReminder.Domain.Entities;

public class Birthday
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Name { get; set; } = default!;
    public int Day { get; set; }
    public int Month { get; set; }
    public int? Year { get; set; }

    public string? Phone { get; set; }
    public string? Note { get; set; }
    public string? ContactId { get; set; }

    public bool NotifyEnabled { get; set; } = true;
    public int NotifyDaysBefore { get; set; } = 0;
    public int NotifyTimeMinutes { get; set; } = 540;

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ClientUpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int Version { get; set; } = 1;
}
