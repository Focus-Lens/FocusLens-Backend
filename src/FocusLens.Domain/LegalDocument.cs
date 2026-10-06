using FocusLens.Domain.Common;

namespace FocusLens.Domain;

public sealed class LegalDocument : AuditableEntity
{
    private LegalDocument()
    {
    }

    public LegalDocument(
        LegalDocumentAudience audience,
        string version,
        string content,
        bool isPublished,
        DateTimeOffset? publishedOnUtc,
        LegalDocumentType type = LegalDocumentType.Terms)
        : base(Guid.CreateVersion7())
    {
        Audience = audience;
        Type = type;
        SetDraftContent(version, content);
        IsPublished = isPublished;
        PublishedOnUtc = publishedOnUtc;
    }

    public LegalDocumentAudience Audience { get; private set; }

    public LegalDocumentType Type { get; private set; }

    public string Version { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public bool IsPublished { get; private set; }

    public DateTimeOffset? PublishedOnUtc { get; private set; }

    public void SetDraftContent(string version, string content)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("Published legal documents are immutable.");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version is required.", nameof(version));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        Version = version.Trim();
        Content = content.Trim();
    }

    public void Publish(DateTimeOffset publishedOnUtc)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("Published legal documents are immutable.");
        }

        IsPublished = true;
        PublishedOnUtc = publishedOnUtc;
    }
}
