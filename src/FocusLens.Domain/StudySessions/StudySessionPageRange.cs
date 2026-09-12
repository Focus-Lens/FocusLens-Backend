using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionPageRange
{
    private StudySessionPageRange()
    {
    }

    private StudySessionPageRange(int fromPage, int toPage)
    {
        FromPage = fromPage;
        ToPage = toPage;
    }

    public int FromPage { get; private set; }

    public int ToPage { get; private set; }

    public static Result<StudySessionPageRange> Create(int fromPage, int toPage)
    {
        if (fromPage < 1)
        {
            return Error.Validation("StudySessions.FromPageInvalid", "From page must be at least 1.");
        }

        if (toPage < fromPage)
        {
            return Error.Validation("StudySessions.ToPageInvalid",
                "To page must be greater than or equal to from page.");
        }

        return new StudySessionPageRange(fromPage, toPage);
    }
}