using Microsoft.AspNetCore.Identity;
using Kanban.Data;

namespace Kanban.Services;

// Bootstraps the Admin role from config on every startup. Solves the chicken-and-egg problem
// of needing an admin panel to create the first admin: an operator lists trusted emails in
// config, and any of those emails that later gets an account (via Entra auto-provisioning or
// manual sign-up) picks up the role on the next restart.
public static class AdminRoleSeeder
{
    public const string AdminRoleName = "Admin";
    public const string AdminPolicyName = "Admin";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(AdminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRoleName));
        }

        var settings = AdminSeedSettings.FromConfiguration(configuration);
        foreach (var email in settings.AdminEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                continue;
            }

            if (!await userManager.IsInRoleAsync(user, AdminRoleName))
            {
                await userManager.AddToRoleAsync(user, AdminRoleName);
            }
        }
    }
}
