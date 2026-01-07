using Microsoft.AspNetCore.Identity;
using System;

namespace BirthdayReminder.Infrastructure.Persistence;

public class AppUser : IdentityUser<Guid>
{
}
