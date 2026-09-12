namespace FocusLens.Application.Common.Interfaces;

public interface IInvitationUrlBuilder
{
    string CreateStudentParentInvitationUrl(string token);

    string CreateChildSetupInvitationUrl(string token);
}