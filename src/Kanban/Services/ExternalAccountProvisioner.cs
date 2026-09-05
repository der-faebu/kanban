using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Kanban.Data;

namespace Kanban.Services;

public enum ExternalSignInOutcome
{
    SignedIn,
    RequiresTwoFactor,
    LockedOut,
    NeedsManualRegistration,
    Failed,
}

public record ExternalSignInResult(ExternalSignInOutcome Outcome, string? ErrorMessage = null)
{
    public static readonly ExternalSignInResult RequiresTwoFactor = new(ExternalSignInOutcome.RequiresTwoFactor);
    public static readonly ExternalSignInResult LockedOut = new(ExternalSignInOutcome.LockedOut);
    public static readonly ExternalSignInResult NeedsManualRegistration = new(ExternalSignInOutcome.NeedsManualRegistration);
    public static readonly ExternalSignInResult SignedIn = new(ExternalSignInOutcome.SignedIn);

    public static ExternalSignInResult Failed(string message) => new(ExternalSignInOutcome.Failed, message);
}

public interface IExternalAccountProvisioner
{
    Task<ExternalSignInResult> SignInOrProvisionAsync(ExternalLoginInfo info);
}

// Provider-agnostic "what happens after an external IdP redirects back" decision: sign in if
// this login is already linked; auto-link-or-create when the provider vouches for a verified
// email (a trusted IdP like Entra); otherwise leave it to the caller to fall back to the
// default UI's manual confirmation form. Kept out of ExternalLogin.razor's code-behind so the
// claims-to-account logic can be exercised directly in tests without a live IdP redirect.
public class ExternalAccountProvisioner(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    ILogger<ExternalAccountProvisioner> logger) : IExternalAccountProvisioner
{
    public async Task<ExternalSignInResult> SignInOrProvisionAsync(ExternalLoginInfo info)
    {
        var signInResult = await TrySignInAsync(info);
        if (signInResult is not null)
        {
            return signInResult;
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            return ExternalSignInResult.NeedsManualRegistration;
        }

        return await ProvisionAsync(info, email);
    }

    private async Task<ExternalSignInResult?> TrySignInAsync(ExternalLoginInfo info)
    {
        // Unlike Identity's scaffolded default UI (which passes bypassTwoFactor: true), this
        // deliberately respects 2FA: an external IdP session shouldn't skip a second factor the
        // account owner explicitly enabled. Safe to change now because external logins were
        // inert until this ticket — no existing user's behavior changes.
        var result = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: false);

        if (result.Succeeded)
        {
            logger.LogInformation(
                "{Name} logged in with {LoginProvider} provider.",
                info.Principal.Identity?.Name,
                info.LoginProvider);
            return ExternalSignInResult.SignedIn;
        }

        if (result.RequiresTwoFactor)
        {
            return ExternalSignInResult.RequiresTwoFactor;
        }

        if (result.IsLockedOut)
        {
            return ExternalSignInResult.LockedOut;
        }

        return null;
    }

    private async Task<ExternalSignInResult> ProvisionAsync(ExternalLoginInfo info, string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        var isNewUser = user is null;

        if (isNewUser)
        {
            user = Activator.CreateInstance<ApplicationUser>();
            await userStore.SetUserNameAsync(user, email, CancellationToken.None);
            await GetEmailStore().SetEmailAsync(user, email, CancellationToken.None);
            user.EmailConfirmed = true;

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return ExternalSignInResult.Failed(FormatErrors(createResult.Errors));
            }
        }

        var addLoginResult = await userManager.AddLoginAsync(user!, info);
        if (!addLoginResult.Succeeded)
        {
            return ExternalSignInResult.Failed(FormatErrors(addLoginResult.Errors));
        }

        logger.LogInformation(
            isNewUser ? "User created an account using {LoginProvider} provider." : "Linked existing account to {LoginProvider} provider.",
            info.LoginProvider);

        return await TrySignInAsync(info) ?? ExternalSignInResult.Failed("Sign-in failed after provisioning.");
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors) =>
        string.Join(",", errors.Select(error => error.Description));

    private IUserEmailStore<ApplicationUser> GetEmailStore()
    {
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("This app requires a user store with email support.");
        }
        return (IUserEmailStore<ApplicationUser>)userStore;
    }
}
