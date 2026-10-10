using FocusLens.Application.Common.Mappings;
using FocusLens.Application.Common.Utilities;
using FocusLens.Application.Parents;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using MediatR;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.ChildSetup;

public sealed class UpdateChildSetupDraftCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
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

        if (request.Request.Grade == ContractStudentGrade.Other)
        {
            if (string.IsNullOrWhiteSpace(request.Request.CustomGrade))
            {
                return Error.Validation(
                    "ChildSetup.CustomGradeRequired",
                    "A custom grade is required.");
            }

            if (request.Request.CustomGrade.Trim().Length > 100)
            {
                return Error.Validation(
                    "ChildSetup.CustomGradeTooLong",
                    "A custom grade cannot exceed 100 characters.");
            }
        }
        else if (request.Request.CustomGrade is not null)
        {
            return Error.Validation(
                "ChildSetup.InvalidCustomGrade",
                "CustomGrade is only allowed when grade is Other.");
        }

        IReadOnlyCollection<StudentSubjectRequest> subjects =
            request.Request.Subjects ?? [];

        if (!AreValidSubjects(subjects, out Error? subjectsError))
        {
            return subjectsError!.Value;
        }

        draft.SetName(
            request.Request.FirstName,
            request.Request.LastName);

        draft.SetDateOfBirth(request.Request.DateOfBirth);

        draft.SetGrade(request.Request.Grade is null
            ? null
            : StudentEnumMapper.ToDomain(request.Request.Grade.Value));
        draft.SetCustomGrade(
            request.Request.Grade == ContractStudentGrade.Other
                ? request.Request.CustomGrade
                : null);
        draft.ReplaceSubjects(subjects.Select(MapSubject));

        draft.SetGoal(
            request.Request.Goal is null
                ? null
                : StudentEnumMapper.ToDomain(request.Request.Goal.Value));

        StudyTimeGoal? studyTimeGoal = null;

        if (request.Request.StudyTimeGoal is not null)
        {
            if (parent.WeekStartsOn is not DayOfWeek weekStartsOn)
            {
                return Error.Validation(
                    "ChildSetup.WeekStartsOnRequired",
                    "Choose a week start day before setting up a study-time goal.");
            }

            Result<int> targetMinutes = ToTargetMinutes(request.Request.StudyTimeGoal.TargetHours);
            if (targetMinutes.IsError)
            {
                return targetMinutes.Errors;
            }

            DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            Result<StudyTimeGoal> studyTimeGoalResult = StudyTimeGoal.Create(
                DomainStudyTimeGoalPeriod.Weekly,
                targetMinutes.Value,
                [weekStartsOn],
                WeekStartCalculator.GetWeekStart(today, weekStartsOn));

            if (studyTimeGoalResult.IsError)
            {
                return studyTimeGoalResult.TopError;
            }

            studyTimeGoal = studyTimeGoalResult.Value;
        }

        draft.SetStudyTimeGoal(studyTimeGoal);

        await unitOfWork.SaveChangesAsync();

        return ToResponse(draft, parent.WeekStartsOn);
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

    private static ChildSetupSubject MapSubject(
        StudentSubjectRequest subject)
    {
        return subject.Type == ContractStudentSubjectType.Other
            ? ChildSetupSubject.Custom(subject.CustomName!)
            : ChildSetupSubject.Predefined(
                StudentEnumMapper.ToDomain(subject.Type));
    }

    private static Result<int> ToTargetMinutes(decimal targetHours)
    {
        if (targetHours <= 0 || targetHours > int.MaxValue / 60m)
        {
            return Error.Validation(
                "ChildSetup.InvalidStudyTimeGoal",
                "TargetHours must be greater than zero and within the supported range.");
        }

        decimal targetMinutes = targetHours * 60m;
        if (decimal.Truncate(targetMinutes) != targetMinutes)
        {
            return Error.Validation(
                "ChildSetup.InvalidStudyTimeGoal",
                "TargetHours must convert to a whole number of minutes.");
        }

        return decimal.ToInt32(targetMinutes);
    }

    private static ChildSetupDraftResponse ToResponse(
        ChildSetupDraft draft,
        DayOfWeek? weekStartsOn)
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
            draft.CustomGrade,
            draft.Subjects
                .Select(subject => new ChildSetupSubjectResponse(
                    subject.Id,
                    subject.Type.ToString(),
                    subject.CustomName))
                .ToList(),
            draft.Goal is null
                ? null
                : StudentEnumMapper.ToContract(draft.Goal.Value),
            draft.StudyTimeGoal is null
                ? null
                : new StudyTimeGoalResponse(
                    draft.StudyTimeGoal.Period.ToString(),
                    draft.StudyTimeGoal.TargetMinutes,
                    weekStartsOn is DayOfWeek value
                        ? ParentWeekdayOrder.OrderDays(draft.StudyTimeGoal.Days, value)
                        : draft.StudyTimeGoal.Days,
                    draft.StudyTimeGoal.StartDate,
                    draft.StudyTimeGoal.EndDate),
            draft.ProfileSetupMode?.ToString(),
            draft.ProfileImageStorageReference);
    }
}