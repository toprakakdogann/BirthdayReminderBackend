using BirthdayReminder.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;

namespace BirthdayReminder.Infrastructure.Persistence;

public class AppDbContext
    : IdentityDbContext<AppUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Birthday> Birthdays => Set<Birthday>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    // Birthdays: kullanıcı bazlı listeleme/sıralama hızlansın
    builder.Entity<Birthday>()
        .HasIndex(x => new { x.UserId, x.Month, x.Day });

    // Soft delete ile sık filtreleyeceksen opsiyonel ama faydalı:
    builder.Entity<Birthday>()
        .HasIndex(x => new { x.UserId, x.IsDeleted });

    // RefreshTokens: token hash ile lookup + tekil olsun
    builder.Entity<RefreshToken>()
        .HasIndex(x => x.TokenHash)
        .IsUnique();

    // İsteğe bağlı: kullanıcıya ait tokenları revoke ederken hızlansın
    builder.Entity<RefreshToken>()
        .HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
}
}
