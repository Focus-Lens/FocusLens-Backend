using FocusLens.Application.Common.Errors;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.Login;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenProvider _tokenProvider;
    private readonly IBaseRepository<Student> _studentRepository;

    public LoginCommandHandler(
        IIdentityService identityService,
        ITokenProvider tokenProvider,
        IBaseRepository<Student> studentRepository)
    {
        _identityService = identityService;
        _tokenProvider = tokenProvider;
        _studentRepository = studentRepository;
    }

    public async Task<Result<AuthResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _identityService.FindByEmailAsync(
            request.Email.Trim());

        if (user is null
            || !await _identityService.CheckPasswordAsync(user, request.Password))
        {
            return ApplicationErrors.Identity.InvalidCredentials;
        }

        if (user.IsDisabled)
        {
            return ApplicationErrors.Identity.UserDisabled;
        }

        if (await _identityService.IsLockedOutAsync(user))
        {
            return ApplicationErrors.Identity.UserLockedOut;
        }

        if (!await _identityService.IsEmailConfirmedAsync(user))
        {
            return ApplicationErrors.Identity.EmailNotConfirmed;
        }

        IReadOnlyCollection<string> roles = await _identityService.GetRolesAsync(user);

        Student? student = null;
        if (roles.Contains(ApplicationRoles.Student, StringComparer.Ordinal))
        {
            student = await _studentRepository.FirstOrDefaultAsync(
                item => item.UserId == user.Id);

            if (student is null)
            {
                return Error.NotFound(
                    "Students.NotFound",
                    "The current user does not have a student profile.");
            }


        }
        TokenPair tokenPair = await _tokenProvider.CreateTokenPairAsync(
            user,
            cancellationToken);

        return user.ToAuthResponse(roles, tokenPair, student);
    }
}