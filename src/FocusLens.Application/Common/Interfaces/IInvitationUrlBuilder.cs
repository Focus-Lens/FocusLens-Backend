namespace FocusLens.Application.Common.Interfaces;

public interface IInvitationUrlBuilder
{
    string CreateParentStudentInvitationUrl(string token);

    string CreateStudentParentInvitationUrl(string token);

    string CreateChildSetupInvitationUrl(string token);
}