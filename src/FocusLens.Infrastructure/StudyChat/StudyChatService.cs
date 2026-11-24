using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.StudyChat;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using Microsoft.Extensions.Options;

namespace FocusLens.Infrastructure.StudyChat;

public sealed class StudyChatService(IBaseRepository<Student> students, IBaseRepository<StudySession> sessions, ICurrentUser currentUser, ITemporaryChatStore store, IStudyChatbotClient chatbot, IOptions<ChatOptions> options, TimeProvider clock) : IStudyChatService
{
    private const int MaxMessage = 500, MaxFile = 5 * 1024 * 1024;
    private static readonly Regex DerivedPageMarker = new(@"^\[Page ([1-9][0-9]*)\][ \t]*\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(Math.Max(1, options.Value.InactivityTtlSeconds));
    public async Task<TemporaryChat?> GetInsideAsync(Guid sessionId, CancellationToken ct)
    {
        Guid student = await StudentIdAsync(ct);
        StudySession session = await EnsureSessionAsync(sessionId, student, ct);
        TemporaryChat? chat = await store.GetInsideAsync(student, sessionId, ct);
        return chat is null ? null : WithStudyMaterial(chat, session);
    }
    public async Task<TemporaryChat> StartInsideAsync(Guid sessionId, CancellationToken ct)
    {
        Guid student = await StudentIdAsync(ct);
        StudySession session = await EnsureSessionAsync(sessionId, student, ct);
        await using IAsyncDisposable guard = await LockAsync($"inside:{student:N}:{sessionId:N}", ct);
        TemporaryChat? existing = await store.GetInsideAsync(student, sessionId, ct);
        if (existing is not null) return WithStudyMaterial(existing, session);
        string text = GetInsideMaterialForPython(session);
        TemporaryChat chat = NewChat(student, TemporaryChatType.Inside, sessionId, Guid.NewGuid().ToString("N"));
        await chatbot.StartSessionAsync(new(chat.AiSessionId!, text), ct);
        if (!await store.CreateAsync(chat, ct)) return await store.GetInsideAsync(student, sessionId, ct) ?? throw new StudyChatException(409, "Could not create chat.");
        return WithStudyMaterial(chat, session);
    }
    public async Task<ChatMessageExchange> SendInsideAsync(Guid sessionId, string message, CancellationToken ct)
    {
        Guid student = await StudentIdAsync(ct); StudySession session = await EnsureSessionAsync(sessionId, student, ct); return await SendAsync(await store.GetInsideAsync(student, sessionId, ct), message, GetInsideMaterialForPython(session), null, ct);
    }
    public async Task EndInsideAsync(Guid sessionId, CancellationToken ct) { Guid student = await StudentIdAsync(ct); await EnsureSessionAsync(sessionId, student, ct); TemporaryChat? chat = await store.GetInsideAsync(student, sessionId, ct); if (chat is not null) await EndAsync(chat, ct); }
    public async Task<TemporaryChat?> GetOutsideAsync(CancellationToken ct) => await store.GetOutsideAsync(await StudentIdAsync(ct), ct);
    public async Task<TemporaryChat> CreateOutsideAsync(CancellationToken ct)
    { Guid student = await StudentIdAsync(ct); await using IAsyncDisposable guard = await LockAsync($"outside:{student:N}", ct); return await store.GetOutsideAsync(student, ct) ?? await CreateOutsideLockedAsync(student, ct); }
    private async Task<TemporaryChat> CreateOutsideLockedAsync(Guid student, CancellationToken ct) { TemporaryChat chat = NewChat(student, TemporaryChatType.Outside, null, null); if (!await store.CreateAsync(chat, ct)) return await store.GetOutsideAsync(student, ct) ?? throw new StudyChatException(409, "Could not create chat."); return chat; }
    public async Task<TemporaryChat> AddAttachmentAsync(string id, ChatAttachmentUpload file, CancellationToken ct)
    {
        ValidateFile(file); TemporaryChat chat = await OwnedOutsideAsync(id, ct); await using IAsyncDisposable guard = await LockAsync($"chat:{id}", ct); chat = await OwnedOutsideAsync(id, ct);
        string? extractedText = await chatbot.ExtractTextAsync(file.ContentType, file.Content, ct);
        if (string.IsNullOrWhiteSpace(extractedText)) throw new StudyChatException(422, "No readable text was found in the attachment.");
        string text = extractedText.Trim();
        string ai = Guid.NewGuid().ToString("N"); await chatbot.StartSessionAsync(new(ai, text), ct);
        TemporaryChat updated = Touch(chat with { AiSessionId = ai, MaterialText = text, Attachment = new(file.FileName, file.ContentType) });
        try { await store.SaveAsync(updated, ct); }
        catch { try { await chatbot.EndSessionAsync(ai, ct); } catch { } throw; }
        if (chat.AiSessionId is not null) try { await chatbot.EndSessionAsync(chat.AiSessionId, ct); } catch { }
        return updated;
    }
    public async Task<TemporaryChat> RemoveAttachmentAsync(string id, CancellationToken ct)
    {
        TemporaryChat chat = await OwnedOutsideAsync(id, ct); await using IAsyncDisposable guard = await LockAsync($"chat:{id}", ct); chat = await OwnedOutsideAsync(id, ct);
        if (chat.Attachment is null) return chat;
        TemporaryChat updated = Touch(chat with { AiSessionId = null, MaterialText = null, Attachment = null });
        await store.SaveAsync(updated, ct);
        if (chat.AiSessionId is not null) try { await chatbot.EndSessionAsync(chat.AiSessionId, ct); } catch { }
        return updated;
    }
    public async Task<ChatMessageExchange> SendOutsideAsync(string id, string message, CancellationToken ct) => await SendAsync(await OwnedOutsideAsync(id, ct), message, null, null, ct);
    public async Task EndOutsideAsync(string id, CancellationToken ct) { TemporaryChat chat = await OwnedOutsideAsync(id, ct); await EndAsync(chat, ct); }
    private async Task<ChatMessageExchange> SendAsync(TemporaryChat? chat, string message, string? insideMaterial, string? subject, CancellationToken ct)
    {
        if (chat is null) throw new StudyChatException(404, "No active chat exists."); ValidateMessage(message); await using IAsyncDisposable guard = await LockAsync($"chat:{chat.ChatId}", ct); chat = await store.GetAsync(chat.ChatId, ct); if (chat is null) throw new StudyChatException(404, "Chat has expired or ended.");
        ChatReply reply;
        try { reply = await chatbot.SendAsync(message.Trim(), chat.AiSessionId, chat.History, null, subject, ct); }
        catch (StudyChatRemoteException ex) when (ex.StatusCode == HttpStatusCode.Conflict && chat.AiSessionId is not null) { string text = insideMaterial ?? chat.MaterialText ?? throw new StudyChatException(409, "Chat material is unavailable."); await chatbot.StartSessionAsync(new(chat.AiSessionId, text), ct); reply = await chatbot.SendAsync(message.Trim(), chat.AiSessionId, chat.History, null, subject, ct); }
        DateTimeOffset now = clock.GetUtcNow();
        TemporaryChatMessage userMessage = new(Guid.NewGuid().ToString("N"), "user", message.Trim(), now);
        TemporaryChatMessage assistantMessage = new(Guid.NewGuid().ToString("N"), "assistant", reply.Reply, now, reply.Category, reply.Blocked, reply.Reason, reply.Pages, reply.InputTokens, reply.OutputTokens, reply.TotalTokens);
        TemporaryChatMessage[] history = chat.History.Append(userMessage).Append(assistantMessage).ToArray();
        await store.SaveAsync(Touch(chat with { History = history }), ct);
        return new ChatMessageExchange(userMessage, assistantMessage);
    }
    private TemporaryChat NewChat(Guid student, TemporaryChatType type, Guid? session, string? ai) { DateTimeOffset now = clock.GetUtcNow(); return new(Guid.NewGuid().ToString("N"), student, type, session, ai, now, now, now + _ttl, []); }
    private static string GetInsideMaterialForPython(StudySession session)
    {
        StudySessionSelection selection = session.Selection ?? throw new StudyChatException(409, "Study material has not been extracted yet.");
        string? extractedText = selection.AiExtractedText;
        if (string.IsNullOrWhiteSpace(extractedText)) throw new StudyChatException(409, "Study material has not been extracted yet.");
        string text = extractedText.Trim();
        return DerivedPageMarker.Replace(text, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int derivedPage)) return match.Value;
            long originalPage = (long)selection.FromPage + derivedPage - 1;
            return originalPage is < 1 or > int.MaxValue
                ? match.Value
                : match.Value.Replace(match.Groups[1].Value, ((int)originalPage).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        });
    }
    private static TemporaryChat WithStudyMaterial(TemporaryChat chat, StudySession session) => chat with
    {
        StudyMaterial = session.Material is null ? null : new(session.Material.FileName, "application/pdf")
    };
    private TemporaryChat Touch(TemporaryChat chat) { DateTimeOffset now = clock.GetUtcNow(); return chat with { LastActivityAt = now, ExpiresAt = now + _ttl }; }
    private async Task EndAsync(TemporaryChat chat, CancellationToken ct) { await store.DeleteAsync(chat, ct); if (chat.AiSessionId is not null) try { await chatbot.EndSessionAsync(chat.AiSessionId, ct); } catch { } }
    private async Task<TemporaryChat> OwnedOutsideAsync(string id, CancellationToken ct) { TemporaryChat? chat = await store.GetAsync(id, ct); if (chat is null || chat.Type != TemporaryChatType.Outside || chat.StudentId != await StudentIdAsync(ct)) throw new StudyChatException(404, "Chat was not found."); return chat; }
    private async Task<Guid> StudentIdAsync(CancellationToken ct) { if (currentUser.UserId is not Guid user) throw new StudyChatException(403, "Student identity is unavailable."); Student? student = await students.FirstOrDefaultAsync(x => x.UserId == user); return student?.Id ?? throw new StudyChatException(403, "Student profile is unavailable."); }
    private async Task<StudySession> EnsureSessionAsync(Guid session, Guid student, CancellationToken ct) => await sessions.FirstOrDefaultAsync(x => x.Id == session && x.StudentId == student, x => x.Selection!, x => x.Material!) ?? throw new StudyChatException(404, "Study session was not found.");
    private async Task<IAsyncDisposable> LockAsync(string name, CancellationToken ct) => await store.TryLockAsync(name, ct) ?? throw new StudyChatException(409, "Another chat operation is in progress. Please retry.");
    private static void ValidateMessage(string? text) { if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxMessage) throw new StudyChatException(400, "Message must contain 1 to 500 characters."); }
    private static void ValidateFile(ChatAttachmentUpload f) { if (f.Content.Length is 0 or > MaxFile || f.ContentType is not ("application/pdf" or "image/png" or "image/jpeg") || !SignatureMatches(f.ContentType, f.Content)) throw new StudyChatException(400, "Attachment must be a PDF, PNG, or JPEG no larger than 5 MiB."); }
    private static bool SignatureMatches(string type, byte[] b) => type switch { "application/pdf" => b.AsSpan().StartsWith("%PDF-"u8), "image/png" => b.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}), "image/jpeg" => b.AsSpan().StartsWith(new byte[]{255,216,255}), _ => false };
}
public sealed class StudyChatException(int statusCode, string message) : Exception(message) { public int StatusCode { get; } = statusCode; }
