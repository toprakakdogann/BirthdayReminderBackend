using BirthdayReminder.Domain.Entities;
using BirthdayReminder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BirthdayReminder.Api.Auth;
using BirthdayReminder.Api.Models;

namespace BirthdayReminder.Api.Controllers;

[ApiController]
[Route("birthdays")]
[Authorize]
public class BirthdaysController : ControllerBase
{
    private readonly AppDbContext _db;

    public BirthdaysController(AppDbContext db)
    {
        _db = db;
    }
    
    private Guid UserId => User.GetUserId();

    [HttpGet]
public async Task<ActionResult<List<BirthdayDto>>> GetAll()
{
    var items = await _db.Birthdays
        .Where(x => x.UserId == UserId && !x.IsDeleted)
        .OrderBy(x => x.Month).ThenBy(x => x.Day)
        .ToListAsync();

    return Ok(items.Select(ToDto).ToList());
}

    [HttpPost]
public async Task<ActionResult<BirthdayDto>> Create(BirthdayUpsertRequest req)
{
    Validate(req);

    var entity = new Birthday
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        Name = req.Name.Trim(),
        Day = req.Day,
        Month = req.Month,
        Year = req.Year,
        Phone = req.Phone,
        Note = req.Note,
        ContactId = req.ContactId,
        NotifyEnabled = req.NotifyEnabled,
        NotifyDaysBefore = req.NotifyDaysBefore,
        NotifyTimeMinutes = req.NotifyTimeMinutes,
        ClientUpdatedAtUtc = req.ClientUpdatedAtUtc,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
        Version = 1
    };

    _db.Birthdays.Add(entity);
    await _db.SaveChangesAsync();

    var dto = ToDto(entity);
    return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
}


    [HttpGet("{id:guid}")]
public async Task<ActionResult<BirthdayDto>> GetById(Guid id)
{
    var entity = await _db.Birthdays
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId && !x.IsDeleted);

    return entity == null ? NotFound() : Ok(ToDto(entity));
}


    [HttpPut("{id:guid}")]
public async Task<ActionResult<BirthdayDto>> Update(Guid id, BirthdayUpsertRequest req)
{
    Validate(req);

    var entity = await _db.Birthdays.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
    if (entity == null || entity.IsDeleted) return NotFound();

    if (req.ClientUpdatedAtUtc <= entity.ClientUpdatedAtUtc)
        return Conflict(new { message = "Outdated update. clientUpdatedAtUtc is older than server." });

    entity.Name = req.Name.Trim();
    entity.Day = req.Day;
    entity.Month = req.Month;
    entity.Year = req.Year;
    entity.Phone = req.Phone;
    entity.Note = req.Note;
    entity.ContactId = req.ContactId;
    entity.NotifyEnabled = req.NotifyEnabled;
    entity.NotifyDaysBefore = req.NotifyDaysBefore;
    entity.NotifyTimeMinutes = req.NotifyTimeMinutes;

    entity.ClientUpdatedAtUtc = req.ClientUpdatedAtUtc;
    entity.UpdatedAtUtc = DateTime.UtcNow;
    entity.Version += 1;

    await _db.SaveChangesAsync();
    return Ok(ToDto(entity));
}


    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Birthdays.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (entity == null) return NotFound();

        entity.IsDeleted = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.ClientUpdatedAtUtc = DateTime.UtcNow;
        entity.Version += 1;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Validate(BirthdayUpsertRequest req)
{
    if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Name is required.");
    if (req.Day is < 1 or > 31) throw new ArgumentException("Day must be 1..31");
    if (req.Month is < 1 or > 12) throw new ArgumentException("Month must be 1..12");
    if (req.NotifyTimeMinutes is < 0 or > 1439) throw new ArgumentException("NotifyTimeMinutes must be 0..1439");
    if (req.NotifyDaysBefore is not (0 or 1 or 3 or 7)) throw new ArgumentException("NotifyDaysBefore must be 0/1/3/7");
}


    private static BirthdayDto ToDto(Birthday x) =>
    new(
        x.Id, x.Name, x.Day, x.Month, x.Year,
        x.Phone, x.Note, x.ContactId,
        x.NotifyEnabled, x.NotifyDaysBefore, x.NotifyTimeMinutes,
        x.IsDeleted, x.Version, x.CreatedAtUtc, x.UpdatedAtUtc, x.ClientUpdatedAtUtc
    );


}
