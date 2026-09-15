using System.Security.Cryptography;
using FocusLens.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace FocusLens.API.Infrastructure;

public sealed class ChildSetupInvitationTokenProtector : IChildSetupInvitationTokenProtector
{
    private const string Purpose = "FocusLens.ChildSetupInvitationToken.v1";

    private readonly IDataProtector _protector;

    public ChildSetupInvitationTokenProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string token) => _protector.Protect(token);

    public string Unprotect(string protectedToken)
    {
        try
        {
            return _protector.Unprotect(protectedToken);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                "The child setup invitation token could not be unprotected.",
                exception);
        }
    }
}
