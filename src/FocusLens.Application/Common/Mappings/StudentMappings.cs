using FocusLens.Contracts.Students;
using FocusLens.Domain;

namespace FocusLens.Application.Common.Mappings;

internal static class StudentMappings
{
    public static StudentResponse ToResponse(this Student student)
        => new(student.Id, student.UserId, student.IsOnboardingCompleted);

    public static StudentDetailsResponse ToDetailsResponse(this Student student)
        => new(
            student.Id,
            student.UserId,
            student.PreferredName,
            student.Goal is null ? null : StudentEnumMapper.ToContract(student.Goal.Value),
            student.Grade is null ? null : StudentEnumMapper.ToContract(student.Grade.Value),
            student.Subjects.Select(subject => new StudentSubjectResponse(
                subject.Id,
                StudentEnumMapper.ToContract(subject.Type),
                subject.CustomName)).ToList(),
            student.IsOnboardingCompleted);
}
