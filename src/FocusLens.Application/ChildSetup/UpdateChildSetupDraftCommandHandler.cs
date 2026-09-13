using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using MediatR;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using ContractStudyPriority = FocusLens.Contracts.Students.StudyPriority;
using DomainStudyPriority = FocusLens.Domain.Students.StudyPriority;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using StudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.ChildSetup;

public sealed class UpdateChildSetupDraftCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateChildSetupDraftCommand, Result<ChildSetupDraftResponse>>
{
    public async Task<Result<ChildSetupDraftResponse>> Handle(
        UpdateChildSetupDraftCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Parents.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(item => item.UserId == userId);

        if (parent is null)
        {
            return Error.NotFound(
                "Parents.NotFound",
                "The current user does not have a parent profile.");
        }

        if (request.DraftId == Guid.Empty)
        {
            return Error.Validation(
                "ChildSetup.InvalidDraftId",
                "The child setup draft ID is invalid.");
        }

        ChildSetupDraft? draft =
            await childSetupDraftRepository.GetByIdAsync(
                request.DraftId,
                item => item.Subjects);

        if (draft is null || draft.ParentId != parent.Id)
        {
            return Error.NotFound(
                "ChildSetup.NotFound",
                "The child setup draft could not be found.");
        }

        if (draft.Status != ChildSetupStatus.Draft)
        {
            return Error.Validation(
                "ChildSetup.DraftNotEditable",
                "Only a draft child setup can be edited.");
        }

        if (string.IsNullOrWhiteSpace(request.Request.FirstName))
        {
            return Error.Validation(
                "ChildSetup.FirstNameRequired",
                "First name is required.");
        }

        if (request.Request.FirstName.Trim().Length > 100)
        {
            return Error.Validation(
                "ChildSetup.FirstNameTooLong",
                "First name cannot exceed 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Request.LastName))
        {
            return Error.Validation(
                "ChildSetup.LastNameRequired",
                "Last name is required.");
        }

        if (request.Request.LastName.Trim().Length > 100)
        {
            return Error.Validation(
                "ChildSetup.LastNameTooLong",
                "Last name cannot exceed 100 characters.");
        }

        if (request.Request.DateOfBirth is { } dateOfBirth &&
            dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Error.Validation(
                "ChildSetup.InvalidDateOfBirth",
                "Date of birth cannot be in the future.");
        }

        if (request.Request.Grade is { } grade &&
            !Enum.IsDefined(grade))
        {
            return Error.Validation(
                "ChildSetup.InvalidGrade",
                "Grade is invalid.");
        }

        IReadOnlyCollection<StudentSubjectRequest> subjects =
            request.Request.Subjects ?? [];

        if (!AreValidSubjects(subjects, out Error? subjectsError))
        {
            return subjectsError!.Value;
        }

        IReadOnlyCollection<ContractStudyPriority> priorities =
            request.Request.StudyPriorities ?? [];

        if (!AreValidPriorities(priorities, out Error? prioritiesError))
        {
            return prioritiesError!.Value;
        }

        if (!TryCreateStudyTimeGoal(
                request.Request.StudyTimeGoal,
                out StudyTimeGoal? studyTimeGoal,
                out Error? studyTimeGoalError))
        {
            return studyTimeGoalError!.Value;
        }

        draft.SetName(
            request.Request.FirstName,
            request.Request.LastName);

        draft.SetDateOfBirth(request.Request.DateOfBirth);

        draft.SetGrade(request.Request.Grade is null
            ? null
            : StudentEnumMapper.ToDomain(request.Request.Grade.Value));
        draft.ReplaceSubjects(subjects.Select(MapSubject));

        draft.ReplaceStudyPriorities(
            priorities.Select(MapPriority));

        draft.SetStudyTimeGoal(studyTimeGoal);

        await unitOfWork.SaveChangesAsync();

        return ToResponse(draft);
    }

    private static bool AreValidSubjects(
        IReadOnlyCollection<StudentSubjectRequest> subjects,
        out Error? error)
    {
        error = null;

        foreach (StudentSubjectRequest subject in subjects)
        {
            if (!Enum.IsDefined(subject.Type))
            {
                error = Error.Validation(
                    "ChildSetup.InvalidSubject",
                    "A subject type is invalid.");
                return false;
            }

            if (subject.Type == ContractStudentSubjectType.Other &&
                string.IsNullOrWhiteSpace(subject.CustomName))
            {
                error = Error.Validation(
                    "ChildSetup.CustomSubjectRequired",
                    "A custom subject name is required.");
                return false;
            }

            if (subject.Type == ContractStudentSubjectType.Other &&
                subject.CustomName!.Trim().Length > 200)
            {
                error = Error.Validation(
                    "ChildSetup.CustomSubjectTooLong",
                    "A custom subject name cannot exceed 200 characters.");
                return false;
            }

            if (subject.Type != ContractStudentSubjectType.Other &&
                subject.CustomName is not null)
            {
                error = Error.Validation(
                    "ChildSetup.InvalidCustomSubject",
                    "CustomName is only allowed for Other.");
                return false;
            }
        }

        bool unique =
            subjects
                .Select(subject =>
                    subject.Type == ContractStudentSubjectType.Other
                        ? $"Other:{subject.CustomName!.Trim().ToUpperInvariant()}"
                        : subject.Type.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == subjects.Count;

        if (!unique)
        {
            error = Error.Validation(
                "ChildSetup.DuplicateSubjects",
                "Subjects must be unique.");
            return false;
        }

        return true;
    }

    private static bool AreValidPriorities(
        IReadOnlyCollection<ContractStudyPriority> priorities,
        out Error? error)
    {
        error = null;

        if (priorities.Any(priority => !Enum.IsDefined(priority)))
        {
            error = Error.Validation(
                "ChildSetup.InvalidPriority",
                "A study priority is invalid.");
            return false;
        }

        if (priorities.Distinct().Count() != priorities.Count)
        {
            error = Error.Validation(
                "ChildSetup.DuplicatePriorities",
                "Study priorities must be unique.");
            return false;
        }

        return true;
    }

    private static bool TryCreateStudyTimeGoal(
        StudyTimeGoalRequest? request,
        out StudyTimeGoal? goal,
        out Error? error)
    {
        goal = null;
        error = null;

        if (request is null)
        {
            return true;
        }

        if (!Enum.IsDefined(request.Period))
        {
            error = Error.Validation(
                "ChildSetup.InvalidStudyTimeGoalPeriod",
                "Study-time goal period is invalid.");
            return false;
        }

        if (request.TargetMinutes <= 0)
        {
            error = Error.Validation(
                "ChildSetup.InvalidStudyTimeGoal",
                "Study-time goal must be greater than zero.");
            return false;
        }

        StudyTimeGoalPeriod period =
            Enum.Parse<StudyTimeGoalPeriod>(
                request.Period.ToString());

        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            period,
            request.TargetMinutes,
            request.Days ?? [],
            request.StartDate);

        if (result.IsError)
        {
            error = result.TopError;
            return false;
        }

        goal = result.Value;

        return true;
    }

    private static DomainStudyPriority MapPriority(
        ContractStudyPriority priority) =>
        Enum.Parse<DomainStudyPriority>(priority.ToString());

    private static ChildSetupSubject MapSubject(
        StudentSubjectRequest subject)
    {
        return subject.Type == ContractStudentSubjectType.Other
            ? ChildSetupSubject.Custom(subject.CustomName!)
            : ChildSetupSubject.Predefined(
                StudentEnumMapper.ToDomain(subject.Type));
    }

    private static ChildSetupDraftResponse ToResponse(
        ChildSetupDraft draft)
    {
        return new ChildSetupDraftResponse(
            draft.Id,
            draft.Status.ToString(),
            draft.FirstName,
            draft.LastName,
            draft.DateOfBirth,
            draft.Grade is null
                ? null
                : StudentEnumMapper.ToContract(draft.Grade.Value),
            draft.Subjects
                .Select(subject => new ChildSetupSubjectResponse(
                    subject.Id,
                    subject.Type.ToString(),
                    subject.CustomName))
                .ToList(),
            draft.StudyPriorities
                .Select(priority => Enum.Parse<ContractStudyPriority>(priority.ToString()))
                .ToList(),
            draft.StudyTimeGoal is null
                ? null
                : new StudyTimeGoalResponse(
                    draft.StudyTimeGoal.Period.ToString(),
                    draft.StudyTimeGoal.TargetMinutes,
                    draft.StudyTimeGoal.Days,
                    draft.StudyTimeGoal.StartDate));
    }
}