using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

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
    public void Setup_WithSubjectDurationMaterialSelectionAndSections_CalculatesStudyTime()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        StudyMaterial material = CreateMaterial(session.StudentId);
        StudyMaterialSection first = StudyMaterialSection.Create(material.Id, "Chapter 1", 8, 1, 3).Value;
        StudyMaterialSection second = StudyMaterialSection.Create(material.Id, "Practice", 12, 4, 6).Value;
        StudyMaterialSection third = StudyMaterialSection.Create(material.Id, "Review", 12, 7, 10).Value;

        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(25).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.SetSelection(StudySessionSelection.Create(session, material, 1, 10).Value).IsSuccess);
        Assert.True(session.SetSelectedSections([first, second, third]).IsSuccess);

        Assert.Equal(subject.Id, session.SelectedSubjectId);
        Assert.Equal(32, session.EstimatedStudyTimeMinutes);
        Assert.Equal(1, session.Selection!.FromPage);
        Assert.Equal(10, session.Selection.ToPage);
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

        Result<Success> result = session.Start(new DateTimeOffset(2026, 9, 8, 10, 30, 0, TimeSpan.Zero));

        Assert.True(result.IsError);
        Assert.Equal(StudySessionErrors.NotReady.Code, result.TopError.Code);
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
        DateTimeOffset startedAtUtc = new(2026, 9, 8, 10, 30, 0, TimeSpan.Zero);

        Assert.True(session.Start(startedAtUtc).IsSuccess);
        Assert.Equal(StudySessionStatus.Active, session.Status);
        Assert.Equal(startedAtUtc, session.StartedAtUtc);
    }

    [Fact]
    public void ActiveSession_PauseAndResume_ExcludesPausedTimeFromRemainingDuration()
    {
        StudySession session = CreateReadyDigitalSession();
        DateTimeOffset startedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        session.Start(startedAt);
        Assert.True(session.Pause(startedAt.AddMinutes(10)).IsSuccess);
        Assert.True(session.Resume(startedAt.AddMinutes(20)).IsSuccess);

        Assert.Equal(StudySessionStatus.Active, session.Status);
        Assert.Equal(TimeSpan.FromMinutes(10), session.GetRemainingDuration(startedAt.AddMinutes(25)));
    }

    [Fact]
    public void PausedSession_CanEndAndBecomesCancelled()
    {
        StudySession session = CreateReadyDigitalSession();
        DateTimeOffset startedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        session.Start(startedAt);
        session.Pause(startedAt.AddMinutes(5));

        Assert.True(session.End(startedAt.AddMinutes(20)).IsSuccess);
        Assert.Equal(StudySessionStatus.Cancelled, session.Status);
        Assert.Equal(startedAt.AddMinutes(20), session.CancelledAtUtc);
    }

    [Fact]
    public void ActiveSession_WhenFocusTargetElapses_RemainsActive()
    {
        StudySession session = CreateReadyDigitalSession();
        DateTimeOffset startedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        session.Start(startedAt);

        Assert.Equal(StudySessionStatus.Active, session.Status);
        Assert.Null(session.CompletedAtUtc);
        Assert.Equal(TimeSpan.Zero, session.GetRemainingDuration(startedAt.AddMinutes(25)));
    }

    [Fact]
    public void ActiveSession_WhenAiEstimatedTimeElapses_RemainsActiveAndReportsOvertime()
    {
        StudySession session = CreateReadyDigitalSession();
        DateTimeOffset startedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        // The selected AI section is estimated at 15 minutes; the legacy
        // focus-duration fallback is 25 minutes and must not control timing.
        Assert.True(session.Start(startedAt).IsSuccess);

        DateTimeOffset afterEstimate = startedAt.AddMinutes(16);

        Assert.Equal(StudySessionStatus.Active, session.Status);
        Assert.Null(session.CompletedAtUtc);
        Assert.Equal(TimeSpan.Zero, session.GetRemainingDuration(afterEstimate));
        Assert.Equal(TimeSpan.FromMinutes(1), session.GetOvertimeDuration(afterEstimate));
    }

    [Fact]
    public void PausedSession_PreservesRemainingFocusTime()
    {
        StudySession session = CreateReadyDigitalSession();
        DateTimeOffset startedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        session.Start(startedAt);
        session.Pause(startedAt.AddMinutes(10));

        Assert.Equal(StudySessionStatus.Paused, session.Status);
        Assert.Equal(TimeSpan.FromMinutes(15), session.GetRemainingDuration(startedAt.AddHours(1)));
    }

    [Fact]
    public void SelectedSection_LeavingItsPageRange_MakesChallengeAvailable()
    {
        StudySession session = CreateReadyDigitalSession();
        StudySessionSelectedSection section = session.Selection!.SelectedSections.Single();
        DateTimeOffset startedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        Assert.True(section.Start(startedAt).IsSuccess);
        Assert.True(section.MakeChallengeAvailable(startedAt.AddMinutes(2)).IsSuccess);

        Assert.Equal(startedAt.AddMinutes(2), section.ChallengeAvailableAtUtc);
        Assert.True(section.IsChallengeAvailable);
        Assert.Null(section.CompletedAtUtc);
        Assert.Equal(15, section.EstimatedDurationMinutes);
    }

    [Fact]
    public void SelectedSection_LeavingItsPageRange_IsIdempotent()
    {
        StudySession session = CreateReadyDigitalSession();
        StudySessionSelectedSection section = session.Selection!.SelectedSections.Single();
        DateTimeOffset startedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        section.Start(startedAt);

        Result<Success> result = section.MakeChallengeAvailable(startedAt.AddMinutes(15));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AvailableChallenge_Postpone_DefersItForExactlyFiveMinutes()
    {
        StudySession session = CreateReadyDigitalSession();
        StudySessionSelectedSection section = session.Selection!.SelectedSections.Single();
        DateTimeOffset now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        Assert.True(section.Start(now).IsSuccess);
        Assert.True(section.MakeChallengeAvailable(now).IsSuccess);
        Assert.True(section.PostponeChallenge(now, TimeSpan.FromMinutes(5)).IsSuccess);

        Assert.Equal(now, section.ChallengePostponedAtUtc);
        Assert.Equal(now.AddMinutes(5), section.ChallengeDeferredUntilUtc);
        Assert.False(section.IsChallengeAvailableAt(now.AddMinutes(4).AddSeconds(59)));
        Assert.True(section.IsChallengeAvailableAt(now.AddMinutes(5)));
        Assert.True(section.PostponeChallenge(now.AddMinutes(5), TimeSpan.FromMinutes(5)).IsError);
    }

    [Fact]
    public void ActiveSession_UpdatesOnlyConfiguredProgress()
    {
        StudySession session = CreateReadyDigitalSession();
        StudyMaterialSection selected = StudyMaterialSection.Create(
            session.StudyMaterialId!.Value, "Chapter 1", 15, 2, 10).Value;
        StudySessionSelection selection = StudySessionSelection.Create(
            session,
            session.Material,
            2,
            10).Value;
        session.SetSelection(selection);
        session.SetSelectedSections([selected]);
        session.MarkReady();
        DateTimeOffset now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
        session.Start(now);

        Result<Success> result = session.UpdateProgress(5, now.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(5, session.CurrentPage);
        Assert.Empty(session.CompletedSections);
        Assert.Equal(now.AddMinutes(2), session.LastActivityAtUtc);
    }

    [Fact]
    public void ChangingDuration_PreservesMaterialAndSections()
    {
        StudySession session = CreateReadyDigitalSession();
        Guid materialId = session.StudyMaterialId!.Value;
        Guid[] sectionIds = session.Selection!.SelectedSections.Select(section => section.StudyMaterialSectionId)
            .ToArray();

        Assert.True(session.SetDuration(50).IsSuccess);

        Assert.Equal(StudySessionStatus.Draft, session.Status);
        Assert.Equal(materialId, session.StudyMaterialId);
        Assert.Equal(sectionIds,
            session.Selection!.SelectedSections.Select(section => section.StudyMaterialSectionId));
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
    public void Selection_KeepsDerivedReferenceSeparateFromMaterial()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;
        StudyMaterial material = CreateMaterial(session.StudentId);
        session.SetStudyMaterial(material);
        StudySessionSelection selection = StudySessionSelection.Create(session, material, 1, 2).Value;

        Assert.True(selection.SetDerivedStorageReference("derived/selection.pdf").IsSuccess);

        Assert.Equal("original/book.pdf", material.StorageReference);
        Assert.Equal("derived/selection.pdf", selection.DerivedStorageReference);
    }

    [Fact]
    public void Selection_RejectsMaterialOtherThanTheSessionMaterial()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;
        StudyMaterial attachedMaterial = CreateMaterial(session.StudentId);
        StudyMaterial differentMaterial = CreateMaterial(session.StudentId);
        session.SetStudyMaterial(attachedMaterial);

        Result<StudySessionSelection> result = StudySessionSelection.Create(session, differentMaterial, 1, 2);

        Assert.True(result.IsError);
        Assert.Equal(StudySessionErrors.SelectionMaterialMismatch.Code, result.TopError.Code);
    }

    [Fact]
    public void SettingANewSelection_ReplacesTheActiveSelection()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;
        StudyMaterial material = CreateMaterial(session.StudentId);
        session.SetStudyMaterial(material);
        StudySessionSelection first = StudySessionSelection.Create(session, material, 1, 2).Value;
        StudySessionSelection replacement = StudySessionSelection.Create(session, material, 3, 4).Value;

        session.SetSelection(first);
        session.SetSelection(replacement);

        Assert.Equal(replacement.Id, session.Selection!.Id);
    }

    private static StudySession CreateReadyDigitalSession()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;
        StudyMaterial material = CreateMaterial(session.StudentId);
        StudyMaterialSection section = StudyMaterialSection.Create(material.Id, "Chapter 1", 15, 1, 10).Value;
        session.SetSubject(StudentSubject.Predefined(StudentSubjectType.Math));
        session.SetDuration(25);
        session.SetStudyMaterial(material);
        session.SetSelection(StudySessionSelection.Create(session, material, 1, 5).Value);
        session.SetSelectedSections([section]);
        session.MarkReady();
        return session;
    }

    private static StudyMaterial CreateMaterial(Guid studentId)
    {
        return StudyMaterial.Create(
            studentId,
            "book.pdf",
            1024,
            50,
            "original/book.pdf",
            StudyMaterialSource.Upload).Value;
    }
}
