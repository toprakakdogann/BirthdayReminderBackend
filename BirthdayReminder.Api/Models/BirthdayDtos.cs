using System;

namespace BirthdayReminder.Api.Models;

public record BirthdayDto(
    Guid Id,
    string Name,
    int Day,
    int Month,
    int? Year,
    string? Phone,
    string? Note,
    string? ContactId,
    bool NotifyEnabled,
    int NotifyDaysBefore,
    int NotifyTimeMinutes,
    bool IsDeleted,
    int Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime ClientUpdatedAtUtc
);

public record BirthdayUpsertRequest(
    string Name,
    int Day,
    int Month,
    int? Year,
    string? Phone,
    string? Note,
    string? ContactId,
    bool NotifyEnabled,
    int NotifyDaysBefore,
    int NotifyTimeMinutes,
    DateTime ClientUpdatedAtUtc
);
