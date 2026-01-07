using System.Security.Claims;

namespace BirthdayReminder.Api.Auth;

public static class UserClaimsExtensions
{
    /// <summary>
    /// JWT'den kullanıcı id'sini okur. Biz token'a "sub" claim'i koyuyoruz.
    /// İstersen ileride NameIdentifier da ekleyebilirsin.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out var id))
            return id;

        var nameId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(nameId) && Guid.TryParse(nameId, out var id2))
            return id2;

        throw new UnauthorizedAccessException("User id claim not found in token.");
    }
}
