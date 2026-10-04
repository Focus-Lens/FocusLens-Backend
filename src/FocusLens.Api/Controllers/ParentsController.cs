using FocusLens.Application.ChildSetup;
using FocusLens.Application.Parents;
using FocusLens.Application.Students;
using FocusLens.Contracts;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/parents")]
[Authorize(Roles = "Parent")]
public sealed class ParentsController(ISender sender) : ApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        ParentResponse? parent = await sender.Send(new GetMyParentQuery(), cancellationToken);

        return parent is null ? NotFound() : Ok(parent);
    }

    [HttpGet("me/settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        Result<ParentSettingsResponse> result = await sender.Send(
            new GetParentSettingsQuery(),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpPut("me/settings")]
    public async Task<IActionResult> UpdateSettings(
        UpdateParentSettingsRequest request,
        CancellationToken cancellationToken
    )
    {
        Result<ParentSettingsResponse> result = await sender.Send(
            new UpdateParentSettingsCommand(request),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpGet("overview/children")]
    public async Task<IActionResult> GetOverviewChildren(CancellationToken cancellationToken)
    {
        IReadOnlyList<ParentOverviewChildResponse> children = await sender.Send(
            new GetParentOverviewChildrenQuery(),
            cancellationToken
        );

        return Ok(children);
    }

    [HttpGet("students/{studentId:guid}/dashboard")]
    public async Task<IActionResult> GetStudentDashboard(
        Guid studentId,
        CancellationToken cancellationToken
    )
    {
        Result<ParentDashboardResponse> result = await sender.Send(
            new GetParentDashboardQuery(studentId),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpPut("students/{studentId:guid}/overview-week")]
    public async Task<IActionResult> SetOverviewWeek(Guid studentId, UpdateParentOverviewWeekRequest request, CancellationToken cancellationToken)
    {
        Result<ParentOverviewWeekResponse> result = await sender.Send(new SetParentOverviewWeekCommand(studentId, request.WeekStart), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("students/{studentId:guid}/overview-weeks")]
    public async Task<IActionResult> GetOverviewWeeks(Guid studentId, CancellationToken cancellationToken)
    {
        Result<ParentOverviewWeeksResponse> result = await sender.Send(new GetParentOverviewWeeksQuery(studentId), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("students/{studentId:guid}/study-goals")]
    public async Task<IActionResult> GetStudentStudyGoals(Guid studentId, CancellationToken cancellationToken)
    {
        Result<ParentStudyGoalsResponse> result = await sender.Send(new GetParentStudyGoalsQuery(studentId), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("students/{studentId:guid}/dashboard/sessions")]
    public async Task<IActionResult> GetStudentDashboardSessions(
        Guid studentId,
        [FromQuery(Name = "status")] string[]? statuses,
        [FromQuery] Guid? subjectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        Result<ParentDashboardSessionHistoryResponse> result = await sender.Send(
            new GetParentDashboardSessionsQuery(studentId, statuses, subjectId, page, pageSize),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpGet("students/{studentId:guid}/dashboard/sessions/export")]
    public async Task<IActionResult> ExportStudentDashboardSessions(
        Guid studentId,
        [FromQuery(Name = "status")] string[]? statuses,
        [FromQuery] Guid? subjectId,
        CancellationToken cancellationToken
    )
    {
        Result<ParentDashboardSessionsCsvExport> result = await sender.Send(
            new ExportParentDashboardSessionsQuery(studentId, statuses, subjectId),
            cancellationToken
        );

        return result.Match(
            export => File(export.Content, export.ContentType, export.FileName),
            Problem
        );
    }

    [HttpPost("students/{studentId:guid}/study-goal-proposals")]
    public async Task<IActionResult> CreateStudyGoalProposal(
        Guid studentId,
        CreateStudyGoalProposalRequest request,
        CancellationToken cancellationToken
    )
    {
        Result<StudyGoalProposalResponse> result = await sender.Send(
            new CreateStudyGoalProposalCommand(studentId, request),
            cancellationToken
        );

        return result.Match(
            proposal => StatusCode(StatusCodes.Status201Created, proposal),
            Problem
        );
    }

    [HttpDelete("study-goal-proposals/{proposalId:guid}")]
    public async Task<IActionResult> CancelStudyGoalProposal(
        Guid proposalId,
        CancellationToken cancellationToken
    )
    {
        Result<Success> result = await sender.Send(
            new CancelStudyGoalProposalCommand(proposalId),
            cancellationToken
        );

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPost("child-setups")]
    public async Task<IActionResult> CreateChildSetup(CancellationToken cancellationToken)
    {
        Result<ChildSetupDraftResponse> result = await sender.Send(
            new CreateChildSetupDraftCommand(),
            cancellationToken
        );

        return result.Match(draft => StatusCode(StatusCodes.Status201Created, draft), Problem);
    }

    [HttpPost("child-setups/{draftId:guid}/invite/link")]
    [EnableRateLimiting("InvitationMutation")]
    public async Task<IActionResult> RegenerateChildInvitationLink(
        Guid draftId,
        CancellationToken cancellationToken
    )
    {
        Result<ChildSetupInvitationResponse> result = await sender.Send(
            new RegenerateChildSetupInvitationLinkCommand(draftId),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpPost("child-setups/{draftId:guid}/invite/link/create")]
    [EnableRateLimiting("InvitationMutation")]
    public async Task<IActionResult> CreateChildInvitationLink(
        Guid draftId,
        CancellationToken cancellationToken
    )
    {
        Result<ChildSetupInvitationResponse> result = await sender.Send(
            new CreateChildSetupLinkInvitationCommand(draftId),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpPost("child-setups/{draftId:guid}/invite/resend")]
    [EnableRateLimiting("InvitationMutation")]
    public async Task<IActionResult> ResendChildInvitation(
        Guid draftId,
        CancellationToken cancellationToken
    )
    {
        Result<ChildSetupInvitationResponse> result = await sender.Send(
            new ResendChildSetupInvitationCommand(draftId),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpPost("child-setups/{draftId:guid}/invite/cancel")]
    public async Task<IActionResult> CancelChildInvitation(
        Guid draftId,
        CancellationToken cancellationToken
    )
    {
        Result<Success> result = await sender.Send(
            new CancelChildSetupInvitationCommand(draftId),
            cancellationToken
        );

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpGet("child-setups/invitations")]
    public async Task<IActionResult> GetMyChildSetupInvitations(CancellationToken cancellationToken)
    {
        IReadOnlyList<ParentChildSetupInvitationResponse> invitations = await sender.Send(
            new GetMyChildSetupInvitationsQuery(),
            cancellationToken
        );

        return Ok(invitations);
    }

    [HttpGet("child-setups/{draftId:guid}")]
    public async Task<IActionResult> GetChildSetup(
        Guid draftId,
        CancellationToken cancellationToken
    )
    {
        Result<ChildSetupDraftResponse> result = await sender.Send(
            new GetChildSetupDraftQuery(draftId),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpPut("child-setups/{draftId:guid}")]
    public async Task<IActionResult> UpdateChildSetup(
        Guid draftId,
        UpdateChildSetupDraftRequest request,
        CancellationToken cancellationToken
    )
    {
        Result<ChildSetupDraftResponse> result = await sender.Send(
            new UpdateChildSetupDraftCommand(draftId, request),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpPut("child-setups/{draftId:guid}/profile-setup-mode")]
    public async Task<IActionResult> SetChildSetupProfileMode(
        Guid draftId,
        SetChildSetupProfileModeRequest request,
        CancellationToken cancellationToken
    )
    {
        Result<bool> result = await sender.Send(
            new SetChildSetupProfileModeCommand(draftId, request.ProfileSetupMode),
            cancellationToken
        );

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpGet("child-setups/{draftId:guid}/profile-image")]
    public async Task<IActionResult> GetChildSetupProfileImage(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        Result<ChildSetupProfileImageFile> result = await sender.Send(
            new GetChildSetupProfileImageQuery(draftId),
            cancellationToken);

        return result.Match(
            image => File(image.Content, image.ContentType),
            Problem);
    }

    [HttpPut("child-setups/{draftId:guid}/profile-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UpdateChildSetupProfileImage(
        Guid draftId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Problem([
                Error.Validation(
                    "ChildSetup.EmptyProfileImage",
                    "The profile image is empty.")
            ]);
        }

        Result<string> result = await sender.Send(
            new UpdateChildSetupDraftProfileImageCommand(
                draftId,
                file.FileName,
                file.Length,
                cancellation => Task.FromResult<Stream>(
                    file.OpenReadStream())),
            cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpPost("child-setups/{draftId:guid}/invite")]
    [EnableRateLimiting("InvitationMutation")]
    public async Task<IActionResult> InviteChild(
        Guid draftId,
        CreateChildSetupInvitationRequest request,
        CancellationToken cancellationToken
    )
    {
        Result<ChildSetupInvitationResponse> result = await sender.Send(
            new CreateChildSetupInvitationCommand(draftId, request),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }
}
