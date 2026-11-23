namespace FocusLens.Application.StudyChat;

public enum TemporaryChatType { Inside, Outside }
public sealed record TemporaryChatMessage(string Id, string Role, string Text, DateTimeOffset CreatedAt, string? Category = null, bool Blocked = false, string? Reason = null, IReadOnlyList<int>? Pages = null, int? InputTokens = null, int? OutputTokens = null, int? TotalTokens = null);
public sealed record TemporaryChatAttachment(string FileName, string ContentType);
public sealed record TemporaryStudyMaterial(string FileName, string ContentType);
public sealed record TemporaryChat(string ChatId, Guid StudentId, TemporaryChatType Type, Guid? StudySessionId, string? AiSessionId, DateTimeOffset CreatedAt, DateTimeOffset LastActivityAt, DateTimeOffset ExpiresAt, IReadOnlyList<TemporaryChatMessage> History, TemporaryChatAttachment? Attachment = null, string? MaterialText = null, TemporaryStudyMaterial? StudyMaterial = null);
public sealed record ChatReply(string Reply, string Category, bool Blocked, string? Reason, IReadOnlyList<int> Pages, int InputTokens, int OutputTokens, int TotalTokens);
public sealed record ChatMessageExchange(TemporaryChatMessage UserMessage, TemporaryChatMessage AssistantMessage);
public sealed record ChatSessionStart(string AiSessionId, string MaterialText, string Subject = "", string LessonTitle = "", string Grade = "");
public sealed record ChatAttachmentUpload(string FileName, string ContentType, byte[] Content);

public interface ITemporaryChatStore
{
    Task<TemporaryChat?> GetAsync(string chatId, CancellationToken cancellationToken);
    Task<TemporaryChat?> GetInsideAsync(Guid studentId, Guid studySessionId, CancellationToken cancellationToken);
    Task<TemporaryChat?> GetOutsideAsync(Guid studentId, CancellationToken cancellationToken);
    Task<bool> CreateAsync(TemporaryChat chat, CancellationToken cancellationToken);
    Task<bool> SaveAsync(TemporaryChat chat, CancellationToken cancellationToken);
    Task DeleteAsync(TemporaryChat chat, CancellationToken cancellationToken);
    Task<IAsyncDisposable?> TryLockAsync(string resource, CancellationToken cancellationToken);
}

public interface IStudyChatbotClient
{
    Task StartSessionAsync(ChatSessionStart request, CancellationToken cancellationToken);
    Task<ChatReply> SendAsync(string message, string? aiSessionId, IReadOnlyList<TemporaryChatMessage> history, string? materialText, string? subject, CancellationToken cancellationToken);
    Task<string> ExtractTextAsync(string contentType, byte[] content, CancellationToken cancellationToken);
    Task EndSessionAsync(string aiSessionId, CancellationToken cancellationToken);
}

public interface IStudyChatService
{
    Task<TemporaryChat?> GetInsideAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<TemporaryChat> StartInsideAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<ChatMessageExchange> SendInsideAsync(Guid sessionId, string message, CancellationToken cancellationToken);
    Task EndInsideAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<TemporaryChat?> GetOutsideAsync(CancellationToken cancellationToken);
    Task<TemporaryChat> CreateOutsideAsync(CancellationToken cancellationToken);
    Task<TemporaryChat> AddAttachmentAsync(string chatId, ChatAttachmentUpload file, CancellationToken cancellationToken);
    Task<TemporaryChat> RemoveAttachmentAsync(string chatId, CancellationToken cancellationToken);
    Task<ChatMessageExchange> SendOutsideAsync(string chatId, string message, CancellationToken cancellationToken);
    Task EndOutsideAsync(string chatId, CancellationToken cancellationToken);
}
