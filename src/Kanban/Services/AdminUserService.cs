using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kanban.Data;

namespace Kanban.Services;

public record AdminUserSummary(
    string Id,
    string Email,
    DateTime CreatedAt,
    bool IsAdmin,
    bool HasPassword,
    bool HasPasskey,
    bool HasEntraLogin);

public interface IAdminUserService
{
    Task<List<AdminUserSummary>> GetUsersAsync();
}

// Read-only projection for the admin "list all users" page. Avoids UserManager's per-user
// GetLoginsAsync/GetPasskeysAsync/IsInRoleAsync calls (one round trip per row each) in favor of
// four bulk queries regardless of how many users exist.
public class AdminUserService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IAdminUserService
{
    public async Task<List<AdminUserSummary>> GetUsersAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var adminUserIds = (await db.UserRoles
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .Where(x => x.Name == AdminRoleSeeder.AdminRoleName)
                .Select(x => x.UserId)
                .ToListAsync())
            .ToHashSet();

        var loginProvidersByUser = (await db.UserLogins
                .Select(l => new { l.UserId, l.LoginProvider })
                .ToListAsync())
            .GroupBy(l => l.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.LoginProvider).ToHashSet());

        var passkeyUserIds = (await db.Set<IdentityUserPasskey<string>>()
                .Select(p => p.UserId)
                .ToListAsync())
            .ToHashSet();

        var users = await db.Users
            .OrderBy(u => u.Email)
            .Select(u => new { u.Id, u.Email, u.CreatedAt, u.PasswordHash })
            .ToListAsync();

        return users.Select(u => new AdminUserSummary(
            u.Id,
            u.Email ?? "",
            u.CreatedAt,
            adminUserIds.Contains(u.Id),
            u.PasswordHash is not null,
            passkeyUserIds.Contains(u.Id),
            loginProvidersByUser.TryGetValue(u.Id, out var providers) && providers.Contains(EntraSettings.SchemeName)
        )).ToList();
    }
}
