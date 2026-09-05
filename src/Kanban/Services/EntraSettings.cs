namespace Kanban.Services;

// Entra is registered as an external login scheme only when a deployment configures it — the
// login page's provider picker (ExternalLoginPicker.razor) already hides itself automatically
// when no external schemes are registered, so this is the single switch that turns the button
// on or off.
public record EntraSettings(string? TenantId, string? ClientId, string? ClientSecret)
{
    public const string SchemeName = "Entra";
    public const string DisplayName = "Microsoft";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);

    public static EntraSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Entra");
        return new EntraSettings(section["TenantId"], section["ClientId"], section["ClientSecret"]);
    }
}
