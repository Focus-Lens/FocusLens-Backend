using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Mappings;
using FocusLens.Application.Common.Services;
using FocusLens.Application.Common.Utilities;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using FocusLens.Domain.Students;
using MediatR;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using DomainStudentGoal = FocusLens.Domain.Students.StudentGoal;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.Students;

public sealed class CompleteStudentOnboardingCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IIdentityService identityService,
    TimeProvider timeProvider,
    IStudentLocalTime? studentLocalTime = null)
    : IRequestHandler<CompleteStudentOnboardingCommand, Result<CompleteStudentOnboardingResponse>>
{
    public async Task<Result<CompleteStudentOnboardingResponse>> Handle(
        CompleteStudentOnboardingCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Students.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == userId,
            student => student.Subjects);

        if (student is null)
        {
            return Error.NotFound(
                "Students.NotFound",
                "The current user does not have a student profile.");
        }

        List<StudentSubject> subjects = (request.Request.Subjects ?? [])
            .Select(MapSubject)
            .ToList();

        DomainStudentGoal? goal = request.Request.Goal is null
            ? null
            : StudentEnumMapper.ToDomain(request.Request.Goal.Value);

        student.CompleteOnboarding(
            goal,
            request.Request.Grade is null
                ? null
                : StudentEnumMapper.ToDomain(request.Request.Grade.Value),
            subjects);

        student.SetCustomGrade(
            request.Request.Grade == ContractStudentGrade.Other
                ? request.Request.CustomGrade
                : null);

        student.SetPreferredName(request.Request.PreferredName);
        student.SetDateOfBirth(request.Request.DateOfBirth);

        if (request.Request.StudyTimeGoal is not null)
        {
            Result<int> targetMinutes = ToTargetMinutes(request.Request.StudyTimeGoal.TargetHours);
            if (targetMinutes.IsError)
            {
                return targetMinutes.Errors;
            }

            IEnumerable<ParentStudentRelationship> activeRelationships =
                await relationshipRepository.GetAllAsync(
                    relationship => relationship.StudentId == student.Id &&
                                    relationship.Status == RelationshipStatus.Active,
                    relationship => relationship.Parent);

            DayOfWeek weekStartsOn = ResolveWeekStartsOn(activeRelationships, student);

            studentLocalTime ??= new StudentLocalTime(timeProvider);
            DateOnly today = studentLocalTime.GetToday(student);
            Result<StudyTimeGoal> studyTimeGoal = StudyTimeGoal.Create(
                DomainStudyTimeGoalPeriod.Weekly,
                targetMinutes.Value,
                [weekStartsOn],
                WeekStartCalculator.GetWeekStart(today, weekStartsOn));

            if (studyTimeGoal.IsError)
            {
                return studyTimeGoal.Errors;
            }

            student.SetStudyTimeGoal(studyTimeGoal.Value);
        }

        await unitOfWork.SaveChangesAsync();

        ApplicationUser? user = await identityService.FindByIdAsync(userId);

        if (user is null)
        {
            return Error.NotFound(
                "Users.NotFound",
                "The current user was not found.");
        }

        if (user.IsDisabled)
        {
            return ApplicationErrors.Identity.UserDisabled;
        }

        if (await identityService.IsLockedOutAsync(user))
        {
            return ApplicationErrors.Identity.UserLockedOut;
        }

        IReadOnlyCollection<string> roles = await identityService.GetRolesAsync(user);

        if (!roles.Contains(ApplicationRoles.Student, StringComparer.Ordinal))
        {
            return Error.Forbidden(
                "Students.CurrentUserIsNotStudent",
                "The current user is not a student.");
        }

        return new CompleteStudentOnboardingResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            student.IsOnboardingCompleted ? "completed" : "incomplete");
    }

    private static StudentSubject MapSubject(StudentSubjectRequest subject)
    {
        return subject.Type == ContractStudentSubjectType.Other
            ? StudentSubject.Custom(subject.CustomName!)
            : StudentSubject.Predefined(StudentEnumMapper.ToDomain(subject.Type));
    }

    private static DayOfWeek ResolveWeekStartsOn(
        IEnumerable<ParentStudentRelationship> relationships,
        Student student)
    {
        DayOfWeek? parentWeekStart = relationships
            .Select(relationship => relationship.Parent.WeekStartsOn)
            .FirstOrDefault(weekStartsOn => weekStartsOn.HasValue);

        return parentWeekStart
               ?? student.WeekStartsOn
               ?? DayOfWeek.Saturday;
    }

    private static Result<int> ToTargetMinutes(decimal targetHours)
    {
        if (targetHours <= 0 || targetHours > int.MaxValue / 60m)
        {
            return Error.Validation(
                "Students.InvalidOnboardingStudyTimeGoal",
                "TargetHours must be greater than zero and within the supported range.");
        }

        decimal targetMinutes = targetHours * 60m;
        if (decimal.Truncate(targetMinutes) != targetMinutes)
        {
            return Error.Validation(
                "Students.InvalidOnboardingStudyTimeGoal",
                "TargetHours must convert to a whole number of minutes.");
        }

        return decimal.ToInt32(targetMinutes);
    }
}