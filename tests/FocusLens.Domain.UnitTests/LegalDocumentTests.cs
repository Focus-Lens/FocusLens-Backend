using FocusLens.Domain;

namespace FocusLens.Domain.UnitTests;

public sealed class LegalDocumentTests
{
    [Fact]
    public void PublishedDocument_CannotBeEditedOrPublishedAgain()
    {
        DateTimeOffset publishedOnUtc = new(2026, 9, 4, 0, 0, 0, TimeSpan.Zero);
        LegalDocument document = new(
            LegalDocumentAudience.Student,
            "2026-09-04",
            "Original terms",
            isPublished: false,
            publishedOnUtc: null);

        document.Publish(publishedOnUtc);

        Assert.Throws<InvalidOperationException>(
            () => document.SetDraftContent("2026-09-05", "Updated terms"));
        Assert.Throws<InvalidOperationException>(
            () => document.Publish(publishedOnUtc.AddDays(1)));
    }
}
