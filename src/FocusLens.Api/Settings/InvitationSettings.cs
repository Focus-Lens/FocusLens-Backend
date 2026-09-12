using System.ComponentModel.DataAnnotations;

namespace FocusLens.Settings;

public sealed class InvitationSettings
{
    public const string SectionName = "InvitationSettings";

    [Required] public string BaseUrl { get; set; } = string.Empty;

    [Required] public string StudentParentInvitationBaseUrl { get; set; } = string.Empty;

    [Required] public string ChildSetupInvitationBaseUrl { get; set; } = string.Empty;
}