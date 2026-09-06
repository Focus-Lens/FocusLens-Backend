using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ContractStudentGoal = FocusLens.Contracts.Students.StudentGoal;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using DomainStudentGrade = FocusLens.Domain.Students.StudentGrade;
using DomainStudentGoal = FocusLens.Domain.Students.StudentGoal;
using DomainStudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;

namespace FocusLens.Application.Common.Mappings;

public static class StudentEnumMapper
{
    public static DomainStudentGoal ToDomain(ContractStudentGoal value) => value switch
    {
        ContractStudentGoal.FocusBetter => DomainStudentGoal.FocusBetter,
        ContractStudentGoal.CatchUp => DomainStudentGoal.CatchUp,
        ContractStudentGoal.PrepareForExams => DomainStudentGoal.PrepareForExams,
        ContractStudentGoal.BuildARoutine => DomainStudentGoal.BuildARoutine
    };

    public static DomainStudentGoal? ToDomain(ContractStudentGoal? value)
        => value.HasValue ? ToDomain(value.Value) : null;

    public static DomainStudentGrade ToDomain(ContractStudentGrade value) => value switch
    {
        ContractStudentGrade.Grade5 => DomainStudentGrade.Grade5,
        ContractStudentGrade.Grade6 => DomainStudentGrade.Grade6,
        ContractStudentGrade.Grade7 => DomainStudentGrade.Grade7,
        ContractStudentGrade.Grade8 => DomainStudentGrade.Grade8,
        ContractStudentGrade.Grade9 => DomainStudentGrade.Grade9,
        ContractStudentGrade.Grade10 => DomainStudentGrade.Grade10,
        ContractStudentGrade.Grade11 => DomainStudentGrade.Grade11,
        ContractStudentGrade.Grade12 => DomainStudentGrade.Grade12,
        ContractStudentGrade.Other => DomainStudentGrade.Other
    };

    public static DomainStudentGrade? ToDomain(ContractStudentGrade? value)
        => value.HasValue ? ToDomain(value.Value) : null;

    public static DomainStudentSubjectType ToDomain(ContractStudentSubjectType value) => value switch
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
        ContractStudentSubjectType.Other => DomainStudentSubjectType.Other
    };

    public static ContractStudentGoal ToContract(DomainStudentGoal value) => value switch
    {
        DomainStudentGoal.FocusBetter => ContractStudentGoal.FocusBetter,
        DomainStudentGoal.CatchUp => ContractStudentGoal.CatchUp,
        DomainStudentGoal.PrepareForExams => ContractStudentGoal.PrepareForExams,
        DomainStudentGoal.BuildARoutine => ContractStudentGoal.BuildARoutine
    };

    public static ContractStudentGrade ToContract(DomainStudentGrade value) => value switch
    {
        DomainStudentGrade.Grade5 => ContractStudentGrade.Grade5,
        DomainStudentGrade.Grade6 => ContractStudentGrade.Grade6,
        DomainStudentGrade.Grade7 => ContractStudentGrade.Grade7,
        DomainStudentGrade.Grade8 => ContractStudentGrade.Grade8,
        DomainStudentGrade.Grade9 => ContractStudentGrade.Grade9,
        DomainStudentGrade.Grade10 => ContractStudentGrade.Grade10,
        DomainStudentGrade.Grade11 => ContractStudentGrade.Grade11,
        DomainStudentGrade.Grade12 => ContractStudentGrade.Grade12,
        DomainStudentGrade.Other => ContractStudentGrade.Other
    };

    public static ContractStudentSubjectType ToContract(DomainStudentSubjectType value) => value switch
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
        DomainStudentSubjectType.Other => ContractStudentSubjectType.Other
    };
}
