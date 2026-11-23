namespace FocusLens.Contracts.Chat;

public sealed record SendChatMessageRequest(string Message);
public sealed record ChatMessageResponse(string Id, string Role, string Text, DateTimeOffset CreatedAt, string? Category, bool Blocked, string? Reason, IReadOnlyList<int>? Pages, int? InputTokens, int? OutputTokens, int? TotalTokens);
public sealed record ChatAttachmentResponse(string FileName, string ContentType);
public sealed record OutsideChatResponse(string ChatId, DateTimeOffset CreatedAt, DateTimeOffset LastActivityAt, DateTimeOffset ExpiresAt, IReadOnlyList<ChatMessageResponse> History, ChatAttachmentResponse? Attachment);
public sealed record InsideStudyMaterialResponse(string FileName, string ContentType);
public sealed record InsideStudyChatResponse(string ChatId, DateTimeOffset CreatedAt, DateTimeOffset LastActivityAt, DateTimeOffset ExpiresAt, IReadOnlyList<ChatMessageResponse> History, InsideStudyMaterialResponse StudyMaterial);
public sealed record ChatMessageResponseBody(ChatMessageResponse UserMessage, ChatMessageResponse AssistantMessage);
