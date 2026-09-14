using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Models;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.GoogleLogin;

public sealed class GoogleLoginCommandHandler
    : IRequestHandler<GoogleLoginCommand, Result<AuthResponse>>
{
    private const string LoginProvider = "Google";

    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IIdentityService _identityService;
    private readonly IBaseRepository<Parent> _parentRepository;
    private readonly IBaseRepository<Student> _studentRepository;
    private readonly ITokenProvider _tokenProvider;
    private readonly IUnitOfWork _unitOfWork;

    public GoogleLoginCommandHandler(
        IGoogleTokenValidator googleTokenValidator,
        IIdentityService identityService,
        ITokenProvider tokenProvider,
        IBaseRepository<Student> studentRepository,
        IBaseRepository<Parent> parentRepository,
        IUnitOfWork unitOfWork)
    {
        _googleTokenValidator = googleTokenValidator;
        _identityService = identityService;
        _tokenProvider = tokenProvider;
        _studentRepository = studentRepository;
        _parentRepository = parentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponse>> Handle(
        GoogleLoginCommand request,
        CancellationToken cancellationToken)
    {
        GoogleUserInfo? googleUser = await _googleTokenValidator.ValidateAsync(
            request.IdToken,
            cancellationToken);

        if (googleUser is null)
        {
            return ApplicationErrors.Identity.InvalidExternalToken;
        }

        if (!googleUser.EmailVerified)
        {
            return ApplicationErrors.Identity.ExternalEmailNotVerified;
        }

        bool accountCreated = false;
        string requiredRole = GetRole(request.AccountType);

        ApplicationUser? user = await _identityService.FindByLoginAsync(
            LoginProvider,
            googleUser.ProviderKey);

        if (user is null)
        {
            user = await _identityService.FindByEmailAsync(googleUser.Email);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    Email = googleUser.Email,
                    UserName = googleUser.Email,
                    FirstName = googleUser.FirstName ?? string.Empty,
                    LastName = googleUser.LastName ?? string.Empty,
                    EmailConfirmed = true
                };

                Result<Success> createResult = await CreateGoogleAccountAsync(
                    user,
                    googleUser.ProviderKey,
                    request.AccountType);

                if (createResult.IsError)
                {
                    return createResult.Errors;
                }

                accountCreated = true;
            }

            if (!accountCreated)
            {
                if (!await HasExpectedAccountTypeAsync(user, requiredRole))
                {
                    return ApplicationErrors.Identity.ExternalAccountTypeMismatch;
                }

                IdentityResultSummary loginResult = await _identityService.AddLoginAsync(
                    user,
                    LoginProvider,
                    googleUser.ProviderKey,
                    LoginProvider);

                if (!loginResult.Succeeded)
                {
                    return loginResult.ToApplicationErrors();
                }
            }
        }

        if (user.IsDisabled)
        {
            return ApplicationErrors.Identity.UserDisabled;
        }

        if (await _identityService.IsLockedOutAsync(user))
        {
            return ApplicationErrors.Identity.UserLockedOut;
        }

        IReadOnlyCollection<string> roles = await _identityService.GetRolesAsync(user);

        if (!roles.Contains(requiredRole, StringComparer.Ordinal))
        {
            return ApplicationErrors.Identity.ExternalAccountTypeMismatch;
        }

        Student? student = null;

        if (roles.Contains(ApplicationRoles.Student, StringComparer.Ordinal))
        {
            student = await _studentRepository.FirstOrDefaultAsync(
                student => student.UserId == user.Id,
                student => student.Subjects);
        }

        if (roles.Contains(ApplicationRoles.Student, StringComparer.Ordinal)
            && student is not null
            && !student.IsOnboardingCompleted)
        {
            if (accountCreated)
            {
                (string Token, DateTimeOffset ExpiresOnUtc) registrationToken =
                    await _tokenProvider.CreateOnboardingTokenAsync(user);

                return new AuthResponse(
                    user.Id,
                    user.Email ?? string.Empty,
                    user.FirstName,
                    user.LastName,
                    roles,
                    null,
                    true,
                    registrationToken.Token,
                    registrationToken.ExpiresOnUtc,
                    true);
            }

            TokenPair incompleteOnboardingTokenPair = await _tokenProvider.CreateTokenPairAsync(
                user,
                cancellationToken);

            return user.ToAuthResponse(
                roles,
                incompleteOnboardingTokenPair,
                false);
        }

        TokenPair tokenPair = await _tokenProvider.CreateTokenPairAsync(
            user,
            cancellationToken);

        return user.ToAuthResponse(
            roles,
            tokenPair,
            student?.IsOnboardingCompleted);
    }

    private async Task<Result<Success>> CreateGoogleAccountAsync(
        ApplicationUser user,
        string providerKey,
        LegalDocumentAudience accountType)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            IdentityResultSummary createResult = await _identityService.CreateAsync(
                user,
                Guid.CreateVersion7().ToString("N") + "Aa1!");

            if (!createResult.Succeeded)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return createResult.ToApplicationErrors();
            }

            string role = GetRole(accountType);

            IdentityResultSummary roleResult = await _identityService.AddToRoleAsync(
                user,
                role);

            if (!roleResult.Succeeded)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return roleResult.ToApplicationErrors();
            }

            IdentityResultSummary loginResult = await _identityService.AddLoginAsync(
                user,
                LoginProvider,
                providerKey,
                LoginProvider);

            if (!loginResult.Succeeded)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return loginResult.ToApplicationErrors();
            }

            if (accountType == LegalDocumentAudience.Parent)
            {
                _parentRepository.Add(new Parent(user.Id));
            }
            else
            {
                _studentRepository.Add(new Student(user.Id));
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return Result.Success;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task<bool> HasExpectedAccountTypeAsync(
        ApplicationUser user,
        string requiredRole)
    {
        IReadOnlyCollection<string> roles = await _identityService.GetRolesAsync(user);

        return roles.Contains(requiredRole, StringComparer.Ordinal);
    }

    private static string GetRole(LegalDocumentAudience accountType)
        => accountType == LegalDocumentAudience.Parent
            ? ApplicationRoles.Parent
            : ApplicationRoles.Student;
}