using FocusLens.Domain.Common;
using FocusLens.Domain.Identity;

namespace FocusLens.Domain.Notifications;

public sealed class UserDeviceToken : AuditableEntity
{
    private UserDeviceToken()
    {
    }

    public UserDeviceToken(Guid userId, DeviceTokenPlatform platform, string token, DateTimeOffset registeredAtUtc)
        : base(Guid.CreateVersion7())
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (!Enum.IsDefined(platform))
        {
            throw new ArgumentOutOfRangeException(nameof(platform), platform, "Device platform is invalid.");
        }

        UserId = userId;
        Platform = platform;
        Token = Normalize(token, nameof(token), 2048);
        RegisteredAtUtc = registeredAtUtc;
        LastSeenAtUtc = registeredAtUtc;
    }

    public Guid UserId { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    public DeviceTokenPlatform Platform { get; private set; }

    public string Token { get; private set; } = string.Empty;

    public DateTimeOffset RegisteredAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public DateTimeOffset? DisabledAtUtc { get; private set; }

    public bool IsDisabled => DisabledAtUtc is not null;

    public void Touch(DeviceTokenPlatform platform, DateTimeOffset seenAtUtc)
    {
        Platform = platform;
        LastSeenAtUtc = seenAtUtc;
        DisabledAtUtc = null;
    }

    public void Disable(DateTimeOffset disabledAtUtc)
    {
        DisabledAtUtc ??= disabledAtUtc;
    }

    private static string Normalize(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        string normalized = value.Trim();
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
