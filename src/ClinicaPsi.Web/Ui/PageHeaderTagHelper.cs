using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ClinicaPsi.Web.Ui;

/// <summary>
/// Cabeçalho compartilhado das áreas logadas.
/// O conteúdo interno vira a faixa de ações (botões, links).
/// </summary>
[HtmlTargetElement("page-header", TagStructure = TagStructure.NormalOrSelfClosing)]
public sealed class PageHeaderTagHelper : TagHelper
{
    public string Title { get; set; } = "";

    public string? Subtitle { get; set; }

    public string? Icon { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var actions = await output.GetChildContentAsync();
        var iconClass = NormalizeIcon(Icon);

        output.TagName = "header";
        output.Attributes.SetAttribute("class", "admin-page-header");

        output.Content.AppendHtml("<div class=\"d-flex justify-content-between align-items-center flex-wrap gap-3\"><div>");
        output.Content.AppendHtml("<h1 class=\"admin-page-title mb-1\">");
        if (iconClass is not null)
        {
            output.Content.AppendHtml("<i class=\"");
            output.Content.Append(iconClass);
            output.Content.AppendHtml(" me-2\"></i>");
        }
        output.Content.Append(Title);
        output.Content.AppendHtml("</h1>");

        if (!string.IsNullOrWhiteSpace(Subtitle))
        {
            output.Content.AppendHtml("<p class=\"admin-page-subtitle mb-0\">");
            output.Content.Append(Subtitle);
            output.Content.AppendHtml("</p>");
        }

        output.Content.AppendHtml("</div>");
        if (!actions.IsEmptyOrWhiteSpace)
        {
            output.Content.AppendHtml("<div class=\"admin-page-actions\">");
            output.Content.AppendHtml(actions);
            output.Content.AppendHtml("</div>");
        }
        output.Content.AppendHtml("</div>");
    }

    private static string? NormalizeIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        var value = icon.Trim();
        if (value.Contains(' ') || value.Contains('"') || value.Contains('<'))
        {
            return null;
        }

        return value.StartsWith("bi-", StringComparison.Ordinal) ? $"bi {value}" : value;
    }
}
