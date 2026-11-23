namespace FocusLens.Infrastructure.StudyChat;
public sealed class StudyChatbotOptions { public const string SectionName = "StudyChatbot"; public string BaseUrl { get; init; } = ""; public string ServiceKey { get; init; } = ""; public int TimeoutSeconds { get; init; } = 90; }
public sealed class ChatOptions { public const string SectionName = "Chat"; public int InactivityTtlSeconds { get; init; } = 10800; }
