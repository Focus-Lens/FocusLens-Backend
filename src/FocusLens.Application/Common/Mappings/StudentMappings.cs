using FocusLens.Contracts.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain;

namespace FocusLens.Application.Common.Mappings;

internal static class StudentMappings
{
    public static StudentResponse ToResponse(this Student student) =>
        new(student.Id, student.UserId, student.IsOnboardingCompleted);

    public static ParentStudentSummaryResponse ToParentSummaryResponse(this Student student)
    {
        return new ParentStudentSummaryResponse(
            student.Id,
            student.PreferredName,
            student.User.FirstName,
            student.User.LastName,
            student.Grade is null
                ? null
                : StudentEnumMapper.ToContract(student.Grade.Value),
            student.Subjects
                .Select(subject => new StudentSubjectResponse(
                    subject.Id,
                    StudentEnumMapper.ToContract(subject.Type),
                    subject.CustomName))
                .ToList(),
            student.IsOnboardingCompleted);
    }

    public static StudentDetailsResponse ToDetailsResponse(this Student student)
    {
        return new StudentDetailsResponse(
            student.Id,
            student.UserId,
            student.PreferredName,
            student.DateOfBirth,
            student.Goals
                .Select(StudentEnumMapper.ToContract)
                .ToList(),
            student.Grade is null
                ? null
                : StudentEnumMapper.ToContract(student.Grade.Value),
            student.Subjects
                .Select(subject => new StudentSubjectResponse(
                    subject.Id,
                    StudentEnumMapper.ToContract(subject.Type),
                    subject.CustomName))
                .ToList(),
            student.StudyPriorities
                .Select(priority => Enum.Parse<StudyPriority>(priority.ToString()))
                .ToList(),
            student.StudyTimeGoal is null
                ? null
                : new StudyTimeGoalResponse(
                    student.StudyTimeGoal.Period.ToString(),
                    student.StudyTimeGoal.TargetMinutes,
                  student.StudyTimeGoal.Days,
                  student.StudyTimeGoal.StartDate),
            student.IsOnboardingCompleted);
    }
}