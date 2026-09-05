namespace Kanban.Services;

public record AdminSeedSettings(IReadOnlyList<string> AdminEmails)
{
    public static AdminSeedSettings FromConfiguration(IConfiguration configuration) =>
        new((configuration.GetSection("AdminEmails").Get<string[]>() ?? [])
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .ToArray());
}
