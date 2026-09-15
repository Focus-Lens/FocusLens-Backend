using FocusLens.Application.BehavioralIntelligence;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.UnitTests.BehavioralIntelligence;

public sealed class BehaviorWindowTimelineCalculatorTests
{
    [Theory]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(15, 3)]
    public void NoPauses_ProducesConsecutiveFullWindows(int minutes, int count)
    {
        DateTimeOffset start = At(10, 0);
        var windows = new BehaviorWindowTimelineCalculator().Calculate(start, [], start.AddMinutes(minutes), false).ToArray();
        Assert.Equal(count, windows.Length);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(i + 1, windows[i].WindowIndex);
            Assert.Equal(start.AddMinutes(i * 5), windows[i].WindowStartUtc);
            Assert.Equal(start.AddMinutes((i + 1) * 5), windows[i].WindowEndUtc);
            Assert.False(windows[i].IsFinal);
        }
    }

    [Fact]
    public void PauseInsideWindow_MapsFiveActiveMinutesAcrossPause()
    {
        var session = Ready(At(10, 0)); session.Pause(At(10, 3)); session.Resume(At(10, 20));
        var windows = new BehaviorWindowTimelineCalculator().Calculate(At(10, 0), Map(session), At(10, 22), false).ToArray();
        Assert.Single(windows); AssertWindow(windows[0], 1, At(10, 0), At(10, 22), false);
    }

    [Fact]
    public void FinalPartialWindow_AndPausedEnd_OnlyContainActiveTime()
    {
        var session = Ready(At(10, 0)); session.Pause(At(10, 2)); session.Resume(At(10, 12)); session.Pause(At(10, 14)); session.End(At(10, 30));
        var windows = new BehaviorWindowTimelineCalculator().Calculate(At(10, 0), Map(session), At(10, 30), true).ToArray();
        Assert.Single(windows); AssertWindow(windows[0], 1, At(10, 0), At(10, 30), true);
    }

    [Fact]
    public void MultiplePauses_ProduceContinuousActiveWindows()
    {
        var session = Ready(At(10, 0)); session.Pause(At(10, 2)); session.Resume(At(10, 10)); session.Pause(At(10, 13)); session.Resume(At(10, 20));
        var windows = new BehaviorWindowTimelineCalculator().Calculate(At(10, 0), Map(session), At(10, 25), true).ToArray();
        Assert.Equal(2, windows.Length);
        AssertWindow(windows[0], 1, At(10, 0), At(10, 13), false);
        AssertWindow(windows[1], 2, At(10, 20), At(10, 25), false);
    }
    [Fact]
    public void UnsortedValidIntervals_AreSortedDeterministically()
    {
        var c=new BehaviorWindowTimelineCalculator(); var start=At(10,0);
        var ordered=c.Calculate(start,[new BehaviorPauseInterval(At(10,2),At(10,3)),new BehaviorPauseInterval(At(10,5),At(10,6))],At(10,12),true);
        var unsorted=c.Calculate(start,[new BehaviorPauseInterval(At(10,5),At(10,6)),new BehaviorPauseInterval(At(10,2),At(10,3))],At(10,12),true);
        Assert.Equal(ordered,unsorted);
    }
    [Theory]
    [InlineData(10, 2, 10, 2)]
    [InlineData(10, 3, 10, 2)]
    [InlineData(9, 59, 10, 1)]
    [InlineData(10, 4, 10, 11)]
    public void InvalidIntervals_FailFast(int sh,int sm,int eh,int em)
    {
        Assert.Throws<ArgumentException>(()=>new BehaviorWindowTimelineCalculator().Calculate(At(10,0),[new BehaviorPauseInterval(At(sh,sm),At(eh,em))],At(10,10),false));
    }
    [Fact]
    public void OverlappingIntervals_FailFast()
    {
        Assert.Throws<ArgumentException>(()=>new BehaviorWindowTimelineCalculator().Calculate(At(10,0),[new BehaviorPauseInterval(At(10,2),At(10,5)),new BehaviorPauseInterval(At(10,4),At(10,6))],At(10,10),false));
    }
    [Fact]
    public void PauseAtCompletedWindowBoundary_NextWindowStartsAtResume()
    {
        DateTimeOffset start = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        StudySession session = CreateReadySession();
        Assert.True(session.Start(start).IsSuccess);
        Assert.True(session.Pause(start.AddMinutes(5)).IsSuccess);
        Assert.True(session.Resume(start.AddMinutes(20)).IsSuccess);

        IReadOnlyCollection<BehaviorWindowTimelineItem> windows = new BehaviorWindowTimelineCalculator().Calculate(
            start, Map(session), start.AddMinutes(25), false);

        Assert.Equal(2, windows.Count);
        Assert.Equal(start, windows.ElementAt(0).WindowStartUtc);
        Assert.Equal(start.AddMinutes(5), windows.ElementAt(0).WindowEndUtc);
        Assert.Equal(start.AddMinutes(20), windows.ElementAt(1).WindowStartUtc);
    }

    private static StudySession CreateReadySession()
    {
        StudySession session = StudySession.Create(Guid.NewGuid(), StudySessionMode.Digital).Value;
        StudentSubject subject = StudentSubject.Predefined(StudentSubjectType.Math);
        StudyMaterial material = StudyMaterial.Create(session.StudentId, "material.pdf", 1, 10, "materials/test.pdf", StudyMaterialSource.Upload).Value;
        StudyMaterialSection section = StudyMaterialSection.Create(material.Id, "Section", 15, 1, 10).Value;
        Assert.True(session.SetSubject(subject).IsSuccess);
        Assert.True(session.SetDuration(30).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.SetSelection(StudySessionSelection.Create(session, material, 1, 10).Value).IsSuccess);
        Assert.True(session.SetSelectedSections([section]).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        return session;
    }

    private static StudySession Ready(DateTimeOffset start) { StudySession s = CreateReadySession(); Assert.True(s.Start(start).IsSuccess); return s; }
    private static DateTimeOffset At(int hour, int minute) => new(2026, 9, 15, hour, minute, 0, TimeSpan.Zero);
    private static void AssertWindow(BehaviorWindowTimelineItem item,int index,DateTimeOffset start,DateTimeOffset end,bool final){Assert.Equal(index,item.WindowIndex);Assert.Equal(start,item.WindowStartUtc);Assert.Equal(end,item.WindowEndUtc);Assert.Equal(final,item.IsFinal);}
    private static BehaviorPauseInterval[] Map(StudySession session) => session.PauseIntervals.Select(x => new BehaviorPauseInterval(x.StartedAtUtc, x.EndedAtUtc!.Value)).ToArray();
}
