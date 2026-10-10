namespace FocusLens.Application.Common.Interfaces;

public interface IChildSetupInvitationTokenProtector
{
    string Protect(string token);

    string Unprotect(string protectedToken);
}