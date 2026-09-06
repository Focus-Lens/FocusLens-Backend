using ContractStudentGoal = FocusLens.Contracts.Students.StudentGoal;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;


using MediatR;

namespace FocusLens.Application.Students;

public sealed class UpdateStudentPreferencesCommandHandler(
    IBaseRepository<Student> studentRepository,
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
            item => item.Subjects);

        if (student is null)
        {
            return Error.NotFound("Students.NotFound", "The student profile was not found.");
        }

        if (!TryUpdateGoal(student, request.Request, out Error? goalError))
        {
            return goalError!.Value;
        }

        if (!TryUpdateGrade(student, request.Request, out Error? gradeError))
        {
            return gradeError!.Value;
        }

        if (!TryUpdatePreferredName(student, request.Request, out Error? preferredNameError))
        {
            return preferredNameError!.Value;
        }

        if (!TryUpdateSubjects(student, request.Request, out Error? subjectsError))
        {
            return subjectsError!.Value;
        }

        await unitOfWork.SaveChangesAsync();

        return student.ToDetailsResponse();
    }

    private static bool HasChanges(UpdateStudentPreferencesRequest request)
        => request.GoalProvided || request.GradeProvided || request.PreferredNameProvided || request.SubjectsProvided;

    private static bool TryUpdateGoal(Student student, UpdateStudentPreferencesRequest request, out Error? error)
    {
        error = null;
        if (!request.GoalProvided) return true;
        if (request.Goal is null) { student.SetGoal(null); return true; }

        ContractStudentGoal goal = request.Goal.Value;
        if (!Enum.IsDefined(goal))
        {
            error = Error.Validation("Students.InvalidGoal", "Goal is invalid.");
            return false;
        }

        student.SetGoal(StudentEnumMapper.ToDomain(goal));
        return true;
    }

    private static bool TryUpdateGrade(Student student, UpdateStudentPreferencesRequest request, out Error? error)
    {
        error = null;
        if (!request.GradeProvided) return true;
        if (request.Grade is null) { student.SetGrade(null); return true; }

        ContractStudentGrade grade = request.Grade.Value;
        if (!Enum.IsDefined(grade))
        {
            error = Error.Validation("Students.InvalidGrade", "Grade is invalid.");
            return false;
        }

        student.SetGrade(StudentEnumMapper.ToDomain(grade));
        return true;
    }

    private static bool TryUpdateSubjects(Student student, UpdateStudentPreferencesRequest request, out Error? error)
    {
        error = null;
        if (!request.SubjectsProvided) return true;
        if (request.Subjects is null)
        {
            error = Error.Validation("Students.InvalidSubjects", "Subjects must be an array.");
            return false;
        }

        IReadOnlyCollection<StudentSubjectRequest> requests = request.Subjects;
        if (!AreValidSubjects(requests, out error)) return false;

        student.ReplaceSubjects(requests.Select(MapSubject));
        return true;
    }

    private static bool TryUpdatePreferredName(Student student, UpdateStudentPreferencesRequest request, out Error? error)
    {
        error = null;
        if (!request.PreferredNameProvided) return true;
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
            error = Error.Validation("Students.PreferredNameTooLong", "Preferred name cannot exceed 100 characters.");
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
                error = Error.Validation("Students.CustomSubjectTooLong", "A custom subject name cannot exceed 200 characters.");
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
        => subject.Type == ContractStudentSubjectType.Other
            ? StudentSubject.Custom(subject.CustomName!)
            : StudentSubject.Predefined(StudentEnumMapper.ToDomain(subject.Type));

}
