using System.Text.Encodings.Web;

namespace FocusLens.API.Infrastructure;

public sealed class EmailTemplateRenderer
{
    private const string TemplatesDirectoryName = "Templates";

    private readonly IWebHostEnvironment _environment;

    public EmailTemplateRenderer(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        string templatePath = Path.Combine(
            _environment.ContentRootPath,
            TemplatesDirectoryName,
            templateName);

        string html = await File.ReadAllTextAsync(
            templatePath,
            cancellationToken);

        foreach (KeyValuePair<string, string> placeholder in placeholders)
        {
            html = html.Replace(
                placeholder.Key,
                HtmlEncoder.Default.Encode(placeholder.Value),
                StringComparison.Ordinal);
        }

        return html;
    }
}