using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Notifications;
using FocusLens.Domain.Students;
using MediatR;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;
using DomainStudentGrade = FocusLens.Domain.Students.StudentGrade;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class UpdateStudentPreferencesCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    INotificationWriter notificationWriter,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateStudentPreferencesCommand, Result<StudentDetailsResponse>>
{
    public async Task<Result<StudentDetailsResponse>> Handle(
        UpdateStudentPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Students.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        if (!HasChanges(request.Request))
        {
            return Error.Validation(
                "Students.NoPreferencesProvided",
                "Provide at least one preference to update.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.User,
            item => item.Subjects);

        if (student is null)
        {
            return Error.NotFound("Students.NotFound", "The student profile was not found.");
        }

        if (!TryUpdateGoal(student, request.Request, out Error? goalError))
        {
            return goalError!.Value;
        }

        if (!TryUpdateDateOfBirth(student, request.Request, out Error? dateOfBirthError))
        {
            return dateOfBirthError!.Value;
        }

        IEnumerable<ParentStudentRelationship> activeRelationships =
            await relationshipRepository.GetAllAsync(relationship => relationship.StudentId == student.Id &&
                                                                     relationship.Status == RelationshipStatus.Active);

        if (!TryUpdateStudyTimeGoal(
                student,
                request.Request,
                activeRelationships.Any(),
                out Error? studyTimeGoalError))
        {
            return studyTimeGoalError!.Value;
        }

        if (!TryUpdateWeekStartsOn(
                student,
                request.Request,
                activeRelationships.Any(),
                out Error? weekStartsOnError))
        {
            return weekStartsOnError!.Value;
        }

        if (!TryUpdateGrade(student, request.Request, out Error? gradeError))
        {
            return gradeError!.Value;
        }

        if (!TryUpdateCustomGrade(student, request.Request, out Error? customGradeError))
        {
            return customGradeError!.Value;
        }

        if (!TryUpdatePreferredName(student, request.Request, out Error? preferredNameError))
        {
            return preferredNameError!.Value;
        }

        bool sharingChanged = WillUpdateSharingPreferences(student, request.Request);
        if (!TryUpdateSharingPreferences(student, request.Request, out Error? sharingError))
        {
            return sharingError!.Value;
        }

        if (!TryUpdateSubjects(student, request.Request, out Error? subjectsError))
        {
            return subjectsError!.Value;
        }

        if (sharingChanged)
        {
            IEnumerable<ParentStudentRelationship> relationships =
                await relationshipRepository.GetAllAsync(
                    relationship => relationship.StudentId == student.Id &&
                                    relationship.Status == RelationshipStatus.Active,
                    relationship => relationship.Parent,
                    relationship => relationship.Parent.User);

            foreach (ParentStudentRelationship relationship in relationships)
            {
                if (relationship.Parent.User.IsDisabled || relationship.Parent.User.DeletedAtUtc is not null)
                {
                    continue;
                }

                await notificationWriter.AddAsync(
                    relationship.Parent.UserId,
                    NotificationAudience.Parent,
                    NotificationCategory.PrivacyInformationUpdated,
                    "Privacy information updated",
                    "A connected student updated what study information is shared with you.",
                    $"/parents/students/{student.Id}/dashboard",
                    "View student",
                    $"student:{student.Id}:privacy:{DateTimeOffset.UtcNow.Ticks}:parent:{relationship.Parent.UserId}");
            }
        }

        await unitOfWork.SaveChangesAsync();

        return student.ToDetailsResponse();
    }

    private static bool HasChanges(UpdateStudentPreferencesRequest request)
    {
        return request.DateOfBirthProvided
               || request.GoalProvided
               || request.StudyTimeGoalProvided
               || request.GradeProvided
               || request.CustomGradeProvided
               || request.PreferredNameProvided
               || request.ShareSessionSummariesWithParentsProvided
               || request.ShareSubjectTrendsWithParentsProvided
               || request.ShareDetailedAnswersWithParentsProvided
               || request.WeekStartsOnProvided
               || request.SubjectsProvided;
    }

    private static bool TryUpdateWeekStartsOn(
        Student student,
        UpdateStudentPreferencesRequest request,
        bool hasActiveParentRelationship,
        out Error? error)
    {
        error = null;

        if (!request.WeekStartsOnProvided)
        {
            return true;
        }

        if (hasActiveParentRelationship)
        {
            error = Error.Forbidden(
                "Students.WeekStartsOnControlledByParent",
                "Week start is controlled by the parent.");
            return false;
        }

        if (request.WeekStartsOn.HasValue && !Enum.IsDefined(request.WeekStartsOn.Value))
        {
            error = Error.Validation(
                "Students.InvalidWeekStartsOn",
                "WeekStartsOn must be a valid day of week.");
            return false;
        }

        student.SetWeekStartsOn(request.WeekStartsOn);
        return true;
    }

    private static bool TryUpdateGoal(
        Student student,
        UpdateStudentPreferencesRequest request,
        out Error? error)
    {
        error = null;

        if (!request.GoalProvided)
        {
            return true;
        }

        if (request.Goal is null)
        {
            student.SetGoal(null);
            return true;
        }

        if (!Enum.IsDefined(request.Goal.Value))
        {
            error = Error.Validation(
                "Students.InvalidGoal",
                "A goal is invalid.");

            return false;
        }

        student.SetGoal(StudentEnumMapper.ToDomain(request.Goal.Value));

        return true;
    }

    private static bool TryUpdateDateOfBirth(
        Student student,
        UpdateStudentPreferencesRequest request,
        out Error? error)
    {
        error = null;

        if (!request.DateOfBirthProvided)
        {
            return true;
        }

        student.SetDateOfBirth(request.DateOfBirth);
        return true;
    }

    private static bool TryUpdateStudyTimeGoal(
        Student student,
        UpdateStudentPreferencesRequest request,
        bool hasActiveParentRelationship,
        out Error? error)
    {
        error = null;

        if (!request.StudyTimeGoalProvided)
        {
            return true;
        }

        if (hasActiveParentRelationship)
        {
            error = Error.Forbidden(
                "Students.StudyTimeGoalControlledByParent",
                "Study time goal is controlled by the parent.");

            return false;
        }

        if (request.StudyTimeGoal is null)
        {
            student.SetStudyTimeGoal(null);
            return true;
        }

        if (!Enum.IsDefined(request.StudyTimeGoal.Period))
        {
            error = Error.Validation(
                "Students.InvalidStudyTimeGoalPeriod",
                "Study time goal period is invalid.");
            return false;
        }

        if (request.StudyTimeGoal.TargetMinutes <= 0)
        {
            error = Error.Validation(
                "Students.InvalidStudyTimeGoal",
                "Study time goal must be greater than zero.");
            return false;
        }

        Result<StudyTimeGoal> result = StudyTimeGoal.Create(
            Enum.Parse<DomainStudyTimeGoalPeriod>(
                request.StudyTimeGoal.Period.ToString()),
            request.StudyTimeGoal.TargetMinutes,
            request.StudyTimeGoal.Days ?? [],
            request.StudyTimeGoal.StartDate);

        if (result.IsError)
        {
            error = result.TopError;
            return false;
        }

        student.SetStudyTimeGoal(result.Value);

        return true;
    }

    private static bool TryUpdateGrade(Student student, UpdateStudentPreferencesRequest request, out Error? error)
    {
        error = null;
        if (!request.GradeProvided)
        {
            return true;
        }

        if (request.Grade is null)
        {
            student.SetGrade(null);
            return true;
        }

        ContractStudentGrade grade = request.Grade.Value;
        if (!Enum.IsDefined(grade))
        {
            error = Error.Validation("Students.InvalidGrade", "Grade is invalid.");
            return false;
        }

        student.SetGrade(StudentEnumMapper.ToDomain(grade));
        return true;
    }

    private static bool TryUpdateSubjects(Student student, UpdateStudentPreferencesRequest request,
        out Error? error)
    {
        error = null;
        if (!request.SubjectsProvided)
        {
            return true;
        }

        if (request.Subjects is null)
        {
            error = Error.Validation("Students.InvalidSubjects", "Subjects must be an array.");
            return false;
        }

        IReadOnlyCollection<StudentSubjectRequest> requests = request.Subjects;
        if (!AreValidSubjects(requests, out error))
        {
            return false;
        }

        student.ReplaceSubjects(requests.Select(MapSubject));
        return true;
    }

    private static bool TryUpdateSharingPreferences(
        Student student,
        UpdateStudentPreferencesRequest request,
        out Error? error)
    {
        error = null;

        if (!request.ShareSessionSummariesWithParentsProvided &&
            !request.ShareSubjectTrendsWithParentsProvided &&
            !request.ShareDetailedAnswersWithParentsProvided)
        {
            return true;
        }

        student.SetParentSharingPreferences(
            request.ShareSessionSummariesWithParentsProvided
                ? request.ShareSessionSummariesWithParents!.Value
                : student.ShareSessionSummariesWithParents,
            request.ShareSubjectTrendsWithParentsProvided
                ? request.ShareSubjectTrendsWithParents!.Value
                : student.ShareSubjectTrendsWithParents,
            request.ShareDetailedAnswersWithParentsProvided
                ? request.ShareDetailedAnswersWithParents!.Value
                : student.ShareDetailedAnswersWithParents);

        return true;
    }

    private static bool WillUpdateSharingPreferences(Student student, UpdateStudentPreferencesRequest request)
    {
        bool summariesChanged = request.ShareSessionSummariesWithParentsProvided &&
                                request.ShareSessionSummariesWithParents !=
                                student.ShareSessionSummariesWithParents;
        bool trendsChanged = request.ShareSubjectTrendsWithParentsProvided &&
                             request.ShareSubjectTrendsWithParents !=
                             student.ShareSubjectTrendsWithParents;
        bool detailedAnswersChanged = request.ShareDetailedAnswersWithParentsProvided &&
                                      request.ShareDetailedAnswersWithParents !=
                                      student.ShareDetailedAnswersWithParents;

        return summariesChanged || trendsChanged || detailedAnswersChanged;
    }

    private static bool TryUpdateCustomGrade(
        Student student,
        UpdateStudentPreferencesRequest request,
        out Error? error)
    {
        error = null;

        if (!request.CustomGradeProvided)
        {
            return true;
        }

        if (request.CustomGrade is null)
        {
            student.SetCustomGrade(null);
            return true;
        }

        if (student.Grade is not DomainStudentGrade.Other)
        {
            error = Error.Validation(
                "Students.InvalidCustomGrade",
                "CustomGrade is only allowed when grade is Other.");
            return false;
        }

        string customGrade = request.CustomGrade.Trim();
        if (customGrade.Length == 0)
        {
            error = Error.Validation(
                "Students.CustomGradeRequired",
                "A custom grade is required.");
            return false;
        }

        if (customGrade.Length > 100)
        {
            error = Error.Validation(
                "Students.CustomGradeTooLong",
                "A custom grade cannot exceed 100 characters.");
            return false;
        }

        student.SetCustomGrade(customGrade);
        return true;
    }

    private static bool TryUpdatePreferredName(Student student, UpdateStudentPreferencesRequest request,
        out Error? error)
    {
        error = null;
        if (!request.PreferredNameProvided)
        {
            return true;
        }

        if (request.PreferredName is null)
        {
            student.SetPreferredName(null);
            return true;
        }

        string preferredName = request.PreferredName.Trim();
        if (preferredName.Length == 0)
        {
            error = Error.Validation("Students.PreferredNameRequired", "Preferred name cannot be empty.");
            return false;
        }

        if (preferredName.Length > 100)
        {
            error = Error.Validation("Students.PreferredNameTooLong",
                "Preferred name cannot exceed 100 characters.");
            return false;
        }

        student.SetPreferredName(preferredName);
        return true;
    }

    private static bool AreValidSubjects(IReadOnlyCollection<StudentSubjectRequest> subjects, out Error? error)
    {
        error = null;
        foreach (StudentSubjectRequest subject in subjects)
        {
            if (!Enum.IsDefined(subject.Type))
            {
                error = Error.Validation("Students.InvalidSubject", "A subject type is invalid.");
                return false;
            }

            if (subject.Type == ContractStudentSubjectType.Other && string.IsNullOrWhiteSpace(subject.CustomName))
            {
                error = Error.Validation("Students.CustomSubjectRequired", "A custom subject name is required.");
                return false;
            }

            if (subject.Type == ContractStudentSubjectType.Other && subject.CustomName!.Length > 200)
            {
                error = Error.Validation("Students.CustomSubjectTooLong",
                    "A custom subject name cannot exceed 200 characters.");
                return false;
            }

            if (subject.Type != ContractStudentSubjectType.Other && subject.CustomName is not null)
            {
                error = Error.Validation("Students.InvalidCustomSubject", "CustomName is only allowed for Other.");
                return false;
            }
        }

        bool unique = subjects.Select(subject => subject.Type == ContractStudentSubjectType.Other
                ? $"Other:{subject.CustomName!.Trim().ToUpperInvariant()}"
                : subject.Type.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() == subjects.Count;

        if (!unique)
        {
            error = Error.Validation("Students.DuplicateSubjects", "Subjects must be unique.");
            return false;
        }

        return true;
    }

    private static StudentSubject MapSubject(StudentSubjectRequest subject)
    {
        return subject.Type == ContractStudentSubjectType.Other
            ? StudentSubject.Custom(subject.CustomName!)
            : StudentSubject.Predefined(StudentEnumMapper.ToDomain(subject.Type));
    }
}