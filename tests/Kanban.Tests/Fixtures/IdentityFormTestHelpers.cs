using AngleSharp.Dom;

namespace Kanban.Tests.Fixtures;

internal static class IdentityFormTestHelpers
{
    public static Dictionary<string, string> GetHiddenFormFields(IDocument document)
    {
        var fields = new Dictionary<string, string>();
        foreach (var input in document.QuerySelectorAll("input[type='hidden']"))
        {
            var name = input.GetAttribute("name");
            if (string.IsNullOrEmpty(name))
                continue;

            fields[name] = input.GetAttribute("value") ?? string.Empty;
        }

        return fields;
    }

    public static Dictionary<string, string> GetHiddenFormFields(IElement form)
    {
        var fields = new Dictionary<string, string>();
        foreach (var input in form.QuerySelectorAll("input[type='hidden']"))
        {
            var name = input.GetAttribute("name");
            if (string.IsNullOrEmpty(name))
                continue;

            fields[name] = input.GetAttribute("value") ?? string.Empty;
        }

        return fields;
    }
}
