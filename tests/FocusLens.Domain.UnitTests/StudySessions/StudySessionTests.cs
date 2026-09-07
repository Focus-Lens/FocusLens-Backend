using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.UnitTests.StudySessions;

public class StudySessionTests
{
    [Theory]
    [InlineData(StudySessionMode.Digital)]
    [InlineData(StudySessionMode.Paper)]
    public void Create_WithValidMode_StartsAsDraft(StudySessionMode mode)
    {
        Result<StudySession> result = StudySession.Create(Guid.NewGuid(), mode);

        Assert.True(result.IsSuccess);
        Assert.Equal(mode, result.Value.Mode);
        Assert.Equal(StudySessionStatus.Draft, result.Value.Status);
    }

    [Fact]
    public void Setup_WithSubjectDurationMaterialRangeAndSections_CalculatesStudyTime()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        StudyMaterial material = CreateMaterial(session.StudentId);
        StudyMaterialSection first = StudyMaterialSection.Create(material.Id, "Chapter 1", 8).Value;
        StudyMaterialSection second = StudyMaterialSection.Create(material.Id, "Practice", 12).Value;
        StudyMaterialSection third = StudyMaterialSection.Create(material.Id, "Review", 12).Value;

        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(25).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.SetPageRange(StudySessionPageRange.Create(1, 10).Value).IsSuccess);
        Assert.True(session.SetSelectedSections([first, second, third]).IsSuccess);

        Assert.Equal(subject.Id, session.SelectedSubjectId);
        Assert.Equal(32, session.EstimatedStudyTimeMinutes);
        Assert.Equal(1, session.PageRange!.FromPage);
        Assert.Equal(10, session.PageRange.ToPage);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 3)]
    public void PageRange_InvalidValues_ReturnsValidationError(int fromPage, int toPage)
    {
        Result<StudySessionPageRange> result = StudySessionPageRange.Create(fromPage, toPage);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Start_WhenSetupIsIncomplete_ReturnsFailure()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;

        Result<Success> result = session.Start();

        Assert.True(result.IsError);
        Assert.Equal(StudySessionStatus.Draft, session.Status);
    }

    [Theory]
    [InlineData(StudySessionMode.Digital)]
    [InlineData(StudySessionMode.Paper)]
    public void MarkReady_WithoutMaterial_ReturnsMaterialRequired(StudySessionMode mode)
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), mode).Value;
        session.SetSubject(StudentSubject.Predefined(StudentSubjectType.Math));
        session.SetDuration(25);

        Result<Success> result = session.MarkReady();

        Assert.True(result.IsError);
        Assert.Equal(StudySessionErrors.MaterialRequired.Code, result.TopError.Code);
        Assert.Equal(StudySessionStatus.Draft, session.Status);
    }

    [Fact]
    public void ReadySession_CanStartAndBecomesActive()
    {
        StudySession session = CreateReadyDigitalSession();

        Assert.True(session.Start().IsSuccess);
        Assert.Equal(StudySessionStatus.Active, session.Status);
    }

    [Fact]
    public void ChangingDuration_PreservesMaterialAndSections()
    {
        StudySession session = CreateReadyDigitalSession();
        Guid materialId = session.StudyMaterialId!.Value;
        Guid[] sectionIds = session.SelectedSections.Select(section => section.StudyMaterialSectionId).ToArray();

        Assert.True(session.SetDuration(50).IsSuccess);

        Assert.Equal(StudySessionStatus.Draft, session.Status);
        Assert.Equal(materialId, session.StudyMaterialId);
        Assert.Equal(sectionIds, session.SelectedSections.Select(section => section.StudyMaterialSectionId));
    }

    [Fact]
    public void ChangingSubject_PreservesMaterial()
    {
        StudySession session = CreateReadyDigitalSession();
        Guid materialId = session.StudyMaterialId!.Value;

        Assert.True(session.SetSubject(StudentSubject.Predefined(StudentSubjectType.English)).IsSuccess);

        Assert.Equal(materialId, session.StudyMaterialId);
    }

    [Fact]
    public void Material_KeepsOriginalReferenceWhenDerivedReferenceIsSet()
    {
        StudyMaterial material = CreateMaterial(Guid.NewGuid());

        Assert.True(material.SetDerivedStorageReference("derived/selection.pdf").IsSuccess);

        Assert.Equal("original/book.pdf", material.StorageReference);
        Assert.Equal("derived/selection.pdf", material.DerivedStorageReference);
    }

    private static StudySession CreateReadyDigitalSession()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;
        StudyMaterial material = CreateMaterial(session.StudentId);
        StudyMaterialSection section = StudyMaterialSection.Create(material.Id, "Chapter 1", 15).Value;
        session.SetSubject(StudentSubject.Predefined(StudentSubjectType.Math));
        session.SetDuration(25);
        session.SetStudyMaterial(material);
        session.SetSelectedSections([section]);
        session.MarkReady();
        return session;
    }

    private static StudyMaterial CreateMaterial(Guid studentId)
        => StudyMaterial.Create(
            studentId,
            "book.pdf",
            1024,
            50,
            "original/book.pdf",
            StudyMaterialSource.Upload).Value;
}
