using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Interfaces;

namespace FocusLens.Application.Common.Errors;

internal static class IdentityResultSummaryExtensions
{
    public static List<Error> ToApplicationErrors(this IdentityResultSummary result)
        => [.. result.Errors.Select(error => ApplicationErrors.Identity.OperationFailed(error))];
}
