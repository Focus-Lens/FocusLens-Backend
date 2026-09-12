using FocusLens.Contracts.Home;
using MediatR;

namespace FocusLens.Application.Home;

public sealed record GetStudyOverviewQuery : IRequest<StudyOverviewResponse?>;

public sealed record GetStudyStreakQuery : IRequest<StudyStreakResponse?>;

public sealed record GetSessionsByDayQuery : IRequest<SessionsByDayResponse?>;