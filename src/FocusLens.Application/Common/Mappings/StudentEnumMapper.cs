using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ContractStudentGoal = FocusLens.Contracts.Students.StudentGoal;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using DomainStudentGrade = FocusLens.Domain.Students.StudentGrade;
using DomainStudentGoal = FocusLens.Domain.Students.StudentGoal;
using DomainStudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;

namespace FocusLens.Application.Common.Mappings;

public static class StudentEnumMapper
{
    public static DomainStudentGoal ToDomain(ContractStudentGoal value)
    {
        return value switch
        {
            ContractStudentGoal.FocusBetter => DomainStudentGoal.FocusBetter,
            ContractStudentGoal.CatchUp => DomainStudentGoal.CatchUp,
            ContractStudentGoal.PrepareForExams => DomainStudentGoal.PrepareForExams,
            ContractStudentGoal.BuildARoutine => DomainStudentGoal.BuildARoutine,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported student goal.")
        };
    }

    public static DomainStudentGoal? ToDomain(ContractStudentGoal? value) =>
        value.HasValue ? ToDomain(value.Value) : null;

    public static DomainStudentGrade ToDomain(ContractStudentGrade value)
    {
        return value switch
        {
            ContractStudentGrade.Grade5 => DomainStudentGrade.Grade5,
            ContractStudentGrade.Grade6 => DomainStudentGrade.Grade6,
            ContractStudentGrade.Grade7 => DomainStudentGrade.Grade7,
            ContractStudentGrade.Grade8 => DomainStudentGrade.Grade8,
            ContractStudentGrade.Grade9 => DomainStudentGrade.Grade9,
            ContractStudentGrade.Grade10 => DomainStudentGrade.Grade10,
            ContractStudentGrade.Grade11 => DomainStudentGrade.Grade11,
            ContractStudentGrade.Grade12 => DomainStudentGrade.Grade12,
            ContractStudentGrade.Other => DomainStudentGrade.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported student grade.")
        };
    }

    public static DomainStudentGrade? ToDomain(ContractStudentGrade? value) =>
        value.HasValue ? ToDomain(value.Value) : null;

    public static DomainStudentSubjectType ToDomain(ContractStudentSubjectType value)
    {
        return value switch
        {
            ContractStudentSubjectType.Math => DomainStudentSubjectType.Math,
            ContractStudentSubjectType.English => DomainStudentSubjectType.English,
            ContractStudentSubjectType.History => DomainStudentSubjectType.History,
            ContractStudentSubjectType.Physics => DomainStudentSubjectType.Physics,
            ContractStudentSubjectType.Chemistry => DomainStudentSubjectType.Chemistry,
            ContractStudentSubjectType.Biology => DomainStudentSubjectType.Biology,
            ContractStudentSubjectType.Geography => DomainStudentSubjectType.Geography,
            ContractStudentSubjectType.Languages => DomainStudentSubjectType.Languages,
            ContractStudentSubjectType.ComputerScience => DomainStudentSubjectType.ComputerScience,
            ContractStudentSubjectType.Other => DomainStudentSubjectType.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported student subject type.")
        };
    }

    public static ContractStudentGoal ToContract(DomainStudentGoal value)
    {
        return value switch
        {
            DomainStudentGoal.FocusBetter => ContractStudentGoal.FocusBetter,
            DomainStudentGoal.CatchUp => ContractStudentGoal.CatchUp,
            DomainStudentGoal.PrepareForExams => ContractStudentGoal.PrepareForExams,
            DomainStudentGoal.BuildARoutine => ContractStudentGoal.BuildARoutine,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported student goal.")
        };
    }

    public static ContractStudentGrade ToContract(DomainStudentGrade value)
    {
        return value switch
        {
            DomainStudentGrade.Grade5 => ContractStudentGrade.Grade5,
            DomainStudentGrade.Grade6 => ContractStudentGrade.Grade6,
            DomainStudentGrade.Grade7 => ContractStudentGrade.Grade7,
            DomainStudentGrade.Grade8 => ContractStudentGrade.Grade8,
            DomainStudentGrade.Grade9 => ContractStudentGrade.Grade9,
            DomainStudentGrade.Grade10 => ContractStudentGrade.Grade10,
            DomainStudentGrade.Grade11 => ContractStudentGrade.Grade11,
            DomainStudentGrade.Grade12 => ContractStudentGrade.Grade12,
            DomainStudentGrade.Other => ContractStudentGrade.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported student grade.")
        };
    }

    public static ContractStudentSubjectType ToContract(DomainStudentSubjectType value)
    {
        return value switch
        {
            DomainStudentSubjectType.Math => ContractStudentSubjectType.Math,
            DomainStudentSubjectType.English => ContractStudentSubjectType.English,
            DomainStudentSubjectType.History => ContractStudentSubjectType.History,
            DomainStudentSubjectType.Physics => ContractStudentSubjectType.Physics,
            DomainStudentSubjectType.Chemistry => ContractStudentSubjectType.Chemistry,
            DomainStudentSubjectType.Biology => ContractStudentSubjectType.Biology,
            DomainStudentSubjectType.Geography => ContractStudentSubjectType.Geography,
            DomainStudentSubjectType.Languages => ContractStudentSubjectType.Languages,
            DomainStudentSubjectType.ComputerScience => ContractStudentSubjectType.ComputerScience,
            DomainStudentSubjectType.Other => ContractStudentSubjectType.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported student subject type.")
        };
    }
}