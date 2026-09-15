namespace FocusLens.Contracts.Notifications;

public sealed record RegisterDeviceTokenRequest(
    string Platform,
    string Token);

public sealed record DeviceTokenResponse(
    Guid Id,
    string Platform,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset LastSeenAtUtc);
