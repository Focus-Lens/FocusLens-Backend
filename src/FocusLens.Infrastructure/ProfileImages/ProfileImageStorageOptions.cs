namespace FocusLens.Infrastructure.ProfileImages;

public sealed class ProfileImageStorageOptions
{
    public const string SectionName = "ProfileImageStorage";

    public string RootPath { get; init; } = "../../storage/profiles";
}
