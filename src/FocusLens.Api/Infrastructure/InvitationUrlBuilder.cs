using FocusLens.Application.Common.Interfaces;
using FocusLens.Settings;
using Microsoft.Extensions.Options;

namespace FocusLens.API.Infrastructure;

public sealed class InvitationUrlBuilder(IOptions<InvitationSettings> settings)
    : IInvitationUrlBuilder
{
    public string CreateParentStudentInvitationUrl(string token) =>
        $"{settings.Value.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(token)}";

    public string CreateStudentParentInvitationUrl(string token) =>
        $"{settings.Value.StudentParentInvitationBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(token)}";

    public string CreateChildSetupInvitationUrl(string token) =>
        $"{settings.Value.ChildSetupInvitationBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(token)}";
}