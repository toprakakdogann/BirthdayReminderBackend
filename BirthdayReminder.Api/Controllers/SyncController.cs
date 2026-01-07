using BirthdayReminder.Domain.Entities;
using BirthdayReminder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BirthdayReminder.Api.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace BirthdayReminder.Api.Controllers;

[ApiController]
[Route("sync")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;

    public SyncController(AppDbContext db)
    {
        _db = db;
    }

    private Guid UserId => User.GetUserId();

    public record BirthdayChangeDto(
    Guid Id,
    string? Name,
    int? Day,
    int? Month,
    int? Year,
    string? Phone,
    string? Note,
    string? ContactId,
    bool? NotifyEnabled,
    int? NotifyDaysBefore,
    int? NotifyTimeMinutes,
    bool IsDeleted,
    DateTime ClientUpdatedAtUtc
);
public record BirthdaySyncDto(
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


public record SyncRequest(DateTime? LastSyncAtUtc, List<BirthdayChangeDto> Changes);

    public record SyncResponse(
        DateTime ServerTimeUtc,
        List<BirthdaySyncDto> Upserts,
        List<Guid> Deletes
    );

    [HttpPost]
[EnableRateLimiting("sync")]
    public async Task<ActionResult<SyncResponse>> Sync(SyncRequest req)
    {
        var userId = UserId;

        // 1) Client değişikliklerini uygula
        foreach (var change in req.Changes ?? new List<BirthdayChangeDto>())
{
    if (!change.IsDeleted)
        ValidateUpsert(change);

    var id = change.Id == Guid.Empty ? Guid.NewGuid() : change.Id;

    var entity = await _db.Birthdays.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

    if (entity == null)
    {
        entity = new Birthday
        {
            Id = id,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Version = 1,
        };

        Apply(entity, change);
        entity.ClientUpdatedAtUtc = change.ClientUpdatedAtUtc;

        _db.Birthdays.Add(entity);
    }
    else
    {
        if (change.ClientUpdatedAtUtc > entity.ClientUpdatedAtUtc)
        {
            Apply(entity, change);

            entity.ClientUpdatedAtUtc = change.ClientUpdatedAtUtc;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.Version += 1;
        }
    }
}


        await _db.SaveChangesAsync();

        // 2) Sunucudaki değişiklikleri geri döndür
        var serverTime = DateTime.UtcNow;
        var since = req.LastSyncAtUtc ?? DateTime.MinValue;

        var changed = await _db.Birthdays
            .Where(x => x.UserId == userId && x.UpdatedAtUtc > since)
            .ToListAsync();

        var upserts = changed
            .Where(x => !x.IsDeleted)
            .Select(ToDto)
            .ToList();

        var deletes = changed
            .Where(x => x.IsDeleted)
            .Select(x => x.Id)
            .ToList();

        return Ok(new SyncResponse(serverTime, upserts, deletes));
    }

    private static void Apply(Birthday entity, BirthdayChangeDto dto)
{
    // Silme ise sadece flag yeter
    if (dto.IsDeleted)
    {
        entity.IsDeleted = true;
        return;
    }

    // Upsert alanları
    entity.Name = (dto.Name ?? "").Trim();
    entity.Day = dto.Day ?? 0;
    entity.Month = dto.Month ?? 0;
    entity.Year = dto.Year;
    entity.Phone = dto.Phone;
    entity.Note = dto.Note;
    entity.ContactId = dto.ContactId;
    entity.NotifyEnabled = dto.NotifyEnabled ?? true;
    entity.NotifyDaysBefore = dto.NotifyDaysBefore ?? 1;
    entity.NotifyTimeMinutes = dto.NotifyTimeMinutes ?? 540;
    entity.IsDeleted = false;
}

private static void ValidateUpsert(BirthdayChangeDto req)
{
    if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Name is required.");
    if (req.Day is null or < 1 or > 31) throw new ArgumentException("Day must be 1..31");
    if (req.Month is null or < 1 or > 12) throw new ArgumentException("Month must be 1..12");

    var time = req.NotifyTimeMinutes ?? 0;
    if (time is < 0 or > 1439) throw new ArgumentException("NotifyTimeMinutes must be 0..1439");

    var daysBefore = req.NotifyDaysBefore ?? 0;
    if (daysBefore is not (0 or 1 or 3 or 7)) throw new ArgumentException("NotifyDaysBefore must be 0/1/3/7");
}



    private static BirthdaySyncDto ToDto(Birthday x) =>
    new(
        x.Id,
        x.Name,
        x.Day,
        x.Month,
        x.Year,
        x.Phone,
        x.Note,
        x.ContactId,
        x.NotifyEnabled,
        x.NotifyDaysBefore,
        x.NotifyTimeMinutes,
        x.IsDeleted,
        x.Version,
        x.CreatedAtUtc,
        x.UpdatedAtUtc,
        x.ClientUpdatedAtUtc
    );

    private static void Validate(BirthdaySyncDto req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Name is required.");
        if (req.Day is < 1 or > 31) throw new ArgumentException("Day must be 1..31");
        if (req.Month is < 1 or > 12) throw new ArgumentException("Month must be 1..12");
        if (req.NotifyTimeMinutes is < 0 or > 1439) throw new ArgumentException("NotifyTimeMinutes must be 0..1439");
        if (req.NotifyDaysBefore is not (0 or 1 or 3 or 7)) throw new ArgumentException("NotifyDaysBefore must be 0/1/3/7");
    }
}
