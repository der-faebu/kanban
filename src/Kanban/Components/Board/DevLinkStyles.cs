namespace Kanban.Components.Board;

public static class DevLinkStyles
{
    public static string ProviderIcon(string url)
    {
        if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return "🐙";

        if (url.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase))
            return "🔷";

        return "🔗";
    }
}
