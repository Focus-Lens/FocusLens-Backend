using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.UnitTests.ChildSetup;

public class ChildSetupSubjectTests
{
    [Fact]
    public void Predefined_WithValidType_CreatesSubject()
    {
        ChildSetupSubject subject =
            ChildSetupSubject.Predefined(StudentSubjectType.Math);

        Assert.Equal(StudentSubjectType.Math, subject.Type);
        Assert.Null(subject.CustomName);
    }

    [Fact]
    public void Predefined_WithOther_Throws() =>
        Assert.Throws<ArgumentException>(() => ChildSetupSubject.Predefined(StudentSubjectType.Other));

    [Fact]
    public void Custom_WithName_CreatesOtherSubject()
    {
        ChildSetupSubject subject =
            ChildSetupSubject.Custom(" Economics ");

        Assert.Equal(StudentSubjectType.Other, subject.Type);
        Assert.Equal("Economics", subject.CustomName);
    }

    [Fact]
    public void Custom_WithEmptyName_Throws() => Assert.Throws<ArgumentException>(() => ChildSetupSubject.Custom(" "));
}