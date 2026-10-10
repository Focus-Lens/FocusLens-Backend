using System.Security.Cryptography;
using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Features.Identity.Options;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.RegisterParent;

public sealed class RegisterParentCommandHandler
    : IRequestHandler<RegisterParentCommand, Result<Success>>
{
    private readonly IEmailVerificationCodeStore _codeStore;
    private readonly IEmailSender _emailSender;
    private readonly IIdentityService _identityService;
    private readonly IBaseRepository<LegalDocument> _legalDocumentRepository;
    private readonly IBaseRepository<Parent> _parentRepository;
    private readonly RegistrationOptions _registrationOptions;
    private readonly IBaseRepository<UserTermsAcceptance> _termsAcceptanceRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterParentCommandHandler(
        IIdentityService identityService,
        IBaseRepository<Parent> parentRepository,
        IBaseRepository<LegalDocument> legalDocumentRepository,
        IBaseRepository<UserTermsAcceptance> termsAcceptanceRepository,
        IUnitOfWork unitOfWork,
        IEmailVerificationCodeStore codeStore,
        IEmailSender emailSender,
        TimeProvider timeProvider,
        RegistrationOptions registrationOptions
    )
    {
        _identityService = identityService;
        _parentRepository = parentRepository;
        _legalDocumentRepository = legalDocumentRepository;
        _termsAcceptanceRepository = termsAcceptanceRepository;
        _unitOfWork = unitOfWork;
        _codeStore = codeStore;
        _emailSender = emailSender;
        _timeProvider = timeProvider;
        _registrationOptions = registrationOptions;
    }

    public async Task<Result<Success>> Handle(
        RegisterParentCommand request,
        CancellationToken cancellationToken
    )
    {
        string email = request.Email.Trim();

        if (await _identityService.FindByEmailAsync(email) is not null)
        {
            return ApplicationErrors.Identity.EmailAlreadyRegistered;
        }

        LegalDocument? terms = await GetCurrentPublishedTermsAsync();

        if (terms is null)
        {
            return ApplicationErrors.Terms.NotFound;
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        TimeSpan codeLifetime = TimeSpan.FromMinutes(
            _registrationOptions.EmailVerificationCodeLifetimeMinutes
        );

        ApplicationUser user = new()
        {
            Email = email,
            UserName = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = false
        };
        user.AcceptTerms(terms.Version, utcNow);

        string code = GenerateCode();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            IdentityResultSummary createResult = await _identityService.CreateAsync(
                user,
                request.Password
            );

            if (!createResult.Succeeded)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return createResult.ToApplicationErrors();
            }

            IdentityResultSummary roleResult = await _identityService.AddToRoleAsync(
                user,
                ApplicationRoles.Parent
            );

            if (!roleResult.Succeeded)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return roleResult.ToApplicationErrors();
            }

            _parentRepository.Add(new Parent(user.Id));
            _termsAcceptanceRepository.Add(new UserTermsAcceptance(
                user.Id,
                terms.Id,
                utcNow));
            await _unitOfWork.SaveChangesAsync();

            await _codeStore.SaveAsync(
                user.Id,
                email,
                code,
                utcNow.Add(codeLifetime),
                cancellationToken
            );

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        await _emailSender.SendEmailVerificationCodeAsync(email, code, codeLifetime, cancellationToken);

        return Result.Success;
    }

    private static string GenerateCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private async Task<LegalDocument?> GetCurrentPublishedTermsAsync()
    {
        IEnumerable<LegalDocument> publishedDocuments =
            await _legalDocumentRepository.GetAllAsync(document => document.Audience == LegalDocumentAudience.Parent
                                                                   && document.Type == LegalDocumentType.Terms
                                                                   && document.IsPublished
                                                                   && document.PublishedOnUtc != null);

        return publishedDocuments
            .OrderByDescending(document => document.PublishedOnUtc)
            .FirstOrDefault();
    }
}