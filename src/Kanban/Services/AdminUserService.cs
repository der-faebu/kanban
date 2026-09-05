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
    bool HasEntraLogin,
    bool IsLockedOut);

public interface IAdminUserService
{
    Task<List<AdminUserSummary>> GetUsersAsync();
    Task SetAdminRoleAsync(string userId, bool isAdmin);
    Task SetLockedOutAsync(string userId, bool lockedOut);
}

public class AdminUserService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    UserManager<ApplicationUser> userManager) : IAdminUserService
{
    // Read-only projection for the admin "list all users" page. Avoids UserManager's per-user
    // GetLoginsAsync/GetPasskeysAsync/IsInRoleAsync calls (one round trip per row each) in favor of
    // four bulk queries regardless of how many users exist.
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
            .Select(u => new { u.Id, u.Email, u.CreatedAt, u.PasswordHash, u.LockoutEnd })
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        return users.Select(u => new AdminUserSummary(
            u.Id,
            u.Email ?? "",
            u.CreatedAt,
            adminUserIds.Contains(u.Id),
            u.PasswordHash is not null,
            passkeyUserIds.Contains(u.Id),
            loginProvidersByUser.TryGetValue(u.Id, out var providers) && providers.Contains(EntraSettings.SchemeName),
            u.LockoutEnd.HasValue && u.LockoutEnd.Value > now
        )).ToList();
    }

    public async Task SetAdminRoleAsync(string userId, bool isAdmin)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        var isCurrentlyAdmin = await userManager.IsInRoleAsync(user, AdminRoleSeeder.AdminRoleName);
        if (isAdmin && !isCurrentlyAdmin)
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, AdminRoleSeeder.AdminRoleName));
        }
        else if (!isAdmin && isCurrentlyAdmin)
        {
            EnsureSucceeded(await userManager.RemoveFromRoleAsync(user, AdminRoleSeeder.AdminRoleName));
        }
    }

    public async Task SetLockedOutAsync(string userId, bool lockedOut)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        // Locking always (re-)enables lockout enforcement first: a user provisioned with
        // LockoutEnabled=false would otherwise ignore the lockout end date entirely.
        if (lockedOut)
        {
            EnsureSucceeded(await userManager.SetLockoutEnabledAsync(user, true));
        }

        EnsureSucceeded(await userManager.SetLockoutEndDateAsync(user, lockedOut ? DateTimeOffset.MaxValue : null));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Identity operation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
