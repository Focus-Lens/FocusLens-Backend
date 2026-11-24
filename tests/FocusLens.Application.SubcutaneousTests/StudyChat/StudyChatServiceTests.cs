using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.StudyChat;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using FocusLens.Infrastructure.StudyChat;
using Microsoft.Extensions.Options;

namespace FocusLens.Application.SubcutaneousTests.StudyChat;

public sealed class StudyChatServiceTests
{
    [Fact]
    public async Task Outside_chat_is_resumed_and_only_successful_message_refreshes_ttl()
    {
        var fixture = new Fixture();
        TemporaryChat chat = await fixture.Service.CreateOutsideAsync(CancellationToken.None);
        DateTimeOffset initialExpiry = chat.ExpiresAt;
        fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(initialExpiry, (await fixture.Service.GetOutsideAsync(CancellationToken.None))!.ExpiresAt);

        await fixture.Service.SendOutsideAsync(chat.ChatId, "Explain mitosis", CancellationToken.None);
        TemporaryChat restored = (await fixture.Service.GetOutsideAsync(CancellationToken.None))!;
        Assert.Equal(2, restored.History.Count);
        Assert.True(restored.ExpiresAt > initialExpiry);
        Assert.Equal(chat.ChatId, (await fixture.Service.CreateOutsideAsync(CancellationToken.None)).ChatId);
    }

    [Fact]
    public async Task Outside_message_exchange_uses_the_same_ids_and_timestamps_as_persisted_history()
    {
        var fixture = new Fixture();
        TemporaryChat chat = await fixture.Service.CreateOutsideAsync(CancellationToken.None);

        ChatMessageExchange exchange = await fixture.Service.SendOutsideAsync(chat.ChatId, "Explain mitosis", CancellationToken.None);
        TemporaryChat persisted = (await fixture.Service.GetOutsideAsync(CancellationToken.None))!;

        Assert.Equal(exchange.UserMessage.Id, persisted.History[^2].Id);
        Assert.Equal(exchange.AssistantMessage.Id, persisted.History[^1].Id);
        Assert.Equal(exchange.UserMessage.CreatedAt, persisted.History[^2].CreatedAt);
        Assert.Equal(exchange.AssistantMessage.CreatedAt, persisted.History[^1].CreatedAt);
    }

    [Fact]
    public async Task Inside_message_exchange_and_legacy_chat_record_resolve_material_and_preserve_history_ids()
    {
        var fixture = new Fixture(withInsideSession: true);
        TemporaryChat legacy = new("legacy", fixture.Student.Id, TemporaryChatType.Inside, fixture.SessionId, "ai", fixture.Clock.GetUtcNow(), fixture.Clock.GetUtcNow(), fixture.Clock.GetUtcNow().AddHours(1), []);
        JsonObject legacyJson = JsonNode.Parse(JsonSerializer.Serialize(legacy))!.AsObject();
        legacyJson.Remove(nameof(TemporaryChat.StudyMaterial));
        fixture.Store.Seed(JsonSerializer.Deserialize<TemporaryChat>(legacyJson.ToJsonString())!);

        TemporaryChat started = await fixture.Service.StartInsideAsync(fixture.SessionId, CancellationToken.None);
        ChatMessageExchange exchange = await fixture.Service.SendInsideAsync(fixture.SessionId, "Explain this", CancellationToken.None);
        TemporaryChat persisted = (await fixture.Service.GetInsideAsync(fixture.SessionId, CancellationToken.None))!;

        Assert.Equal("lesson.pdf", started.StudyMaterial!.FileName);
        Assert.Equal("application/pdf", started.StudyMaterial.ContentType);
        Assert.Equal(exchange.UserMessage.Id, persisted.History[^2].Id);
        Assert.Equal(exchange.AssistantMessage.Id, persisted.History[^1].Id);
        Assert.Equal(exchange.UserMessage.CreatedAt, persisted.History[^2].CreatedAt);
        Assert.Equal(exchange.AssistantMessage.CreatedAt, persisted.History[^1].CreatedAt);
        Assert.Equal("lesson.pdf", persisted.StudyMaterial!.FileName);
    }

    [Theory]
    [InlineData(10, 12, "[Page 1]\nFirst\n[Page 2]\nSecond\n[Page 3]\nThird", "[Page 10]\nFirst\n[Page 11]\nSecond\n[Page 12]\nThird")]
    [InlineData(10, 12, "[Page 1]\nFirst\n[Page 3]\nThird", "[Page 10]\nFirst\n[Page 12]\nThird")]
    [InlineData(4, 7, "[Page 1]\nCourse roadmap\n[Page 2]\nFirst lesson\n[Page 3]\nSecond lesson\n[Page 4]\nThird lesson", "[Page 4]\nCourse roadmap\n[Page 5]\nFirst lesson\n[Page 6]\nSecond lesson\n[Page 7]\nThird lesson")]
    [InlineData(1, 3, "[Page 1]\nFirst\n[Page 3]\nThird", "[Page 1]\nFirst\n[Page 3]\nThird")]
    [InlineData(25, 25, "[Page 1]\nOnly", "[Page 25]\nOnly")]
    [InlineData(10, 10, "See [Page 1] in the text.\n[Page 1]\nLesson", "See [Page 1] in the text.\n[Page 10]\nLesson")]
    public async Task Inside_chat_start_maps_only_derived_page_markers_to_original_pdf_pages(int fromPage, int toPage, string extractedText, string expectedMaterial)
    {
        var fixture = new Fixture(withInsideSession: true, fromPage: fromPage, toPage: toPage, extractedText: extractedText);

        await fixture.Service.StartInsideAsync(fixture.SessionId, CancellationToken.None);

        Assert.Equal(expectedMaterial, Assert.Single(fixture.Client.StartRequests).MaterialText);
        Assert.Equal(extractedText, fixture.Session!.Selection!.AiExtractedText);
    }

    [Fact]
    public async Task Inside_chat_409_recovery_uses_the_same_original_page_mapping()
    {
        const string extractedText = "[Page 1]\nFirst\n[Page 3]\nThird";
        const string expectedMaterial = "[Page 10]\nFirst\n[Page 12]\nThird";
        var fixture = new Fixture(withInsideSession: true, fromPage: 10, toPage: 12, extractedText: extractedText) { Client = { ReturnConflictOnce = true } };

        await fixture.Service.StartInsideAsync(fixture.SessionId, CancellationToken.None);
        await fixture.Service.SendInsideAsync(fixture.SessionId, "Explain this", CancellationToken.None);

        Assert.Equal(2, fixture.Client.StartRequests.Count);
        Assert.All(fixture.Client.StartRequests, request => Assert.Equal(expectedMaterial, request.MaterialText));
    }

    [Fact]
    public async Task Inside_chat_post_and_get_message_pages_are_consistent()
    {
        var fixture = new Fixture(withInsideSession: true, fromPage: 10, toPage: 12, extractedText: "[Page 1]\nFirst\n[Page 3]\nThird") { Client = { ReplyPages = [10, 12] } };
        await fixture.Service.StartInsideAsync(fixture.SessionId, CancellationToken.None);

        ChatMessageExchange exchange = await fixture.Service.SendInsideAsync(fixture.SessionId, "Explain this", CancellationToken.None);
        TemporaryChat persisted = (await fixture.Service.GetInsideAsync(fixture.SessionId, CancellationToken.None))!;

        Assert.Equal([10, 12], exchange.AssistantMessage.Pages);
        Assert.Equal(exchange.AssistantMessage.Pages, persisted.History[^1].Pages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t\r\n")]
    public async Task Inside_chat_rejects_missing_empty_or_whitespace_only_extracted_material(string? extractedText)
    {
        var fixture = new Fixture(withInsideSession: true);
        SetPersistedExtractedText(fixture.Session!.Selection!, extractedText);

        StudyChatException error = await Assert.ThrowsAsync<StudyChatException>(() =>
            fixture.Service.StartInsideAsync(fixture.SessionId, CancellationToken.None));

        Assert.Equal(409, error.StatusCode);
        Assert.Empty(fixture.Client.StartRequests);
    }

    // Domain methods correctly reject this data. This simulates a legacy/corrupt value hydrated
    // from persistence, so the service's defensive check is tested without weakening invariants.
    private static void SetPersistedExtractedText(StudySessionSelection selection, string? extractedText)
    {
        PropertyInfo property = typeof(StudySessionSelection).GetProperty(nameof(StudySessionSelection.AiExtractedText))!;
        property.SetValue(selection, extractedText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chatbot_timeout_is_not_retried_or_persisted_for_outside_or_inside_chat(bool inside)
    {
        var fixture = new Fixture(withInsideSession: inside) { Client = { ThrowTimeout = true } };
        TemporaryChat chat = inside
            ? await fixture.Service.StartInsideAsync(fixture.SessionId, CancellationToken.None)
            : await fixture.Service.CreateOutsideAsync(CancellationToken.None);

        if (inside)
        {
            await Assert.ThrowsAsync<StudyChatTimeoutException>(() =>
                fixture.Service.SendInsideAsync(fixture.SessionId, "Explain this", CancellationToken.None));
            Assert.Empty((await fixture.Service.GetInsideAsync(fixture.SessionId, CancellationToken.None))!.History);
        }
        else
        {
            await Assert.ThrowsAsync<StudyChatTimeoutException>(() =>
                fixture.Service.SendOutsideAsync(chat.ChatId, "Explain this", CancellationToken.None));
            Assert.Empty((await fixture.Service.GetOutsideAsync(CancellationToken.None))!.History);
        }

        Assert.Single(fixture.Client.SentSessionIds);
    }

    [Fact]
    public async Task Attachment_is_validated_stored_temporarily_and_409_recovers_without_second_ocr()
    {
        var fixture = new Fixture { Client = { ReturnConflictOnce = true } };
        TemporaryChat chat = await fixture.Service.CreateOutsideAsync(CancellationToken.None);
        await fixture.Service.AddAttachmentAsync(chat.ChatId, new("lesson.pdf", "application/pdf", "%PDF- lesson"u8.ToArray()), CancellationToken.None);
        await fixture.Service.SendOutsideAsync(chat.ChatId, "What is this?", CancellationToken.None);

        TemporaryChat saved = (await fixture.Service.GetOutsideAsync(CancellationToken.None))!;
        Assert.NotNull(saved.Attachment);
        Assert.Equal("OCR text", saved.MaterialText);
        Assert.Equal(1, fixture.Client.OcrCalls);
        Assert.Equal(2, fixture.Client.StartCalls); // initial material session + 409 recovery
        Assert.All(fixture.Client.StartRequests, request => Assert.Equal("OCR text", request.MaterialText));
    }

    [Fact]
    public async Task New_chat_end_then_create_removes_old_history_and_mapping()
    {
        var fixture = new Fixture();
        TemporaryChat oldChat = await fixture.Service.CreateOutsideAsync(CancellationToken.None);
        await fixture.Service.SendOutsideAsync(oldChat.ChatId, "hello", CancellationToken.None);
        await fixture.Service.EndOutsideAsync(oldChat.ChatId, CancellationToken.None);
        TemporaryChat fresh = await fixture.Service.CreateOutsideAsync(CancellationToken.None);
        Assert.NotEqual(oldChat.ChatId, fresh.ChatId);
        Assert.Empty(fresh.History);
        Assert.Null(await fixture.Store.GetAsync(oldChat.ChatId, CancellationToken.None));
    }

    [Fact]
    public async Task Inactive_outside_chat_expires_after_the_configured_three_hour_window()
    {
        var fixture = new Fixture();
        TemporaryChat expired = await fixture.Service.CreateOutsideAsync(CancellationToken.None);
        fixture.Clock.Advance(TimeSpan.FromHours(3).Add(TimeSpan.FromSeconds(1)));

        Assert.Null(await fixture.Service.GetOutsideAsync(CancellationToken.None));
        TemporaryChat fresh = await fixture.Service.CreateOutsideAsync(CancellationToken.None);
        Assert.NotEqual(expired.ChatId, fresh.ChatId);
        Assert.Empty(fresh.History);
        Assert.Null(fresh.Attachment);
    }

    [Fact]
    public async Task Attachment_can_be_added_after_text_and_replaces_only_ai_material_context()
    {
        var fixture = new Fixture();
        TemporaryChat chat = await fixture.Service.CreateOutsideAsync(CancellationToken.None);
        await fixture.Service.SendOutsideAsync(chat.ChatId, "text-only question", CancellationToken.None);
        await fixture.Service.AddAttachmentAsync(chat.ChatId, new("first.pdf", "application/pdf", "%PDF- first"u8.ToArray()), CancellationToken.None);
        TemporaryChat first = (await fixture.Service.GetOutsideAsync(CancellationToken.None))!;
        string firstAiSession = first.AiSessionId!;

        await fixture.Service.AddAttachmentAsync(chat.ChatId, new("second.pdf", "application/pdf", "%PDF- second"u8.ToArray()), CancellationToken.None);
        TemporaryChat replaced = (await fixture.Service.GetOutsideAsync(CancellationToken.None))!;

        Assert.Equal(2, replaced.History.Count);
        Assert.Equal("second.pdf", replaced.Attachment!.FileName);
        Assert.NotEqual(firstAiSession, replaced.AiSessionId);
        Assert.Contains(firstAiSession, fixture.Client.EndedSessionIds);
        await fixture.Service.SendOutsideAsync(chat.ChatId, "question about new file", CancellationToken.None);
        Assert.Equal(replaced.AiSessionId, fixture.Client.SentSessionIds.Last());
    }

    [Fact]
    public async Task Removing_attachment_preserves_history_and_returns_chat_to_text_only_mode()
    {
        var fixture = new Fixture();
        TemporaryChat chat = await fixture.Service.CreateOutsideAsync(CancellationToken.None);
        await fixture.Service.AddAttachmentAsync(chat.ChatId, new("lesson.pdf", "application/pdf", "%PDF- lesson"u8.ToArray()), CancellationToken.None);
        await fixture.Service.SendOutsideAsync(chat.ChatId, "question", CancellationToken.None);
        TemporaryChat removed = await fixture.Service.RemoveAttachmentAsync(chat.ChatId, CancellationToken.None);

        Assert.Null(removed.Attachment);
        Assert.Null(removed.MaterialText);
        Assert.Null(removed.AiSessionId);
        Assert.Equal(2, removed.History.Count);
        await fixture.Service.SendOutsideAsync(chat.ChatId, "text-only again", CancellationToken.None);
        Assert.Null(fixture.Client.SentSessionIds.Last());
    }

    private sealed class Fixture
    {
        public FakeClock Clock { get; } = new();
        public MemoryStore Store { get; }
        public FakeClient Client { get; } = new();
        public IStudyChatService Service { get; }
        public Student Student { get; }
        public Guid SessionId { get; }
        public StudySession? Session { get; }
        public Fixture(bool withInsideSession = false, int fromPage = 1, int toPage = 1, string extractedText = "lesson text")
        {
            Guid user = Guid.NewGuid();
            Store = new MemoryStore(Clock.GetUtcNow);
            Student = new Student(user);
            StudySession? session = null;
            if (withInsideSession)
            {
                session = StudySession.Create(Student.Id, StudySessionMode.Digital).Value;
                StudyMaterial material = StudyMaterial.Create(Student.Id, "lesson.pdf", 1, toPage, "original.pdf", StudyMaterialSource.Upload).Value;
                Assert.True(session.SetStudyMaterial(material).IsSuccess);
                StudySessionSelection selection = StudySessionSelection.Create(session, material, fromPage, toPage).Value;
                Assert.True(selection.SetAiAnalysis(extractedText, "{}").IsSuccess);
                Assert.True(session.SetSelection(selection).IsSuccess);
                SessionId = session.Id;
            }
            Session = session;
            Service = new StudyChatService(new StudentRepository(Student), new SessionRepository(session), new CurrentUser(user), Store, Client, Options.Create(new ChatOptions { InactivityTtlSeconds = 10800 }), Clock);
        }
    }
    private sealed class FakeClock : TimeProvider { private DateTimeOffset _now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => _now; public void Advance(TimeSpan duration) => _now += duration; }
    private sealed class CurrentUser(Guid id) : ICurrentUser { public Guid? UserId => id; public string? Email => null; }
    private sealed class FakeClient : IStudyChatbotClient
    {
        public int OcrCalls { get; private set; } public int StartCalls { get; private set; } public bool ReturnConflictOnce { get; set; } public bool ThrowTimeout { get; set; } public IReadOnlyList<int> ReplyPages { get; set; } = [];
        public List<ChatSessionStart> StartRequests { get; } = [];
        public List<string> EndedSessionIds { get; } = []; public List<string?> SentSessionIds { get; } = [];
        public Task<string> ExtractTextAsync(string contentType, byte[] content, CancellationToken ct) { OcrCalls++; return Task.FromResult("OCR text"); }
        public Task StartSessionAsync(ChatSessionStart request, CancellationToken ct) { StartCalls++; StartRequests.Add(request); return Task.CompletedTask; }
        public Task EndSessionAsync(string aiSessionId, CancellationToken ct) { EndedSessionIds.Add(aiSessionId); return Task.CompletedTask; }
        public Task<ChatReply> SendAsync(string message, string? aiSessionId, IReadOnlyList<TemporaryChatMessage> history, string? materialText, string? subject, CancellationToken ct)
        { SentSessionIds.Add(aiSessionId); if (ThrowTimeout) throw new StudyChatTimeoutException(); if (ReturnConflictOnce) { ReturnConflictOnce = false; throw new StudyChatRemoteException(System.Net.HttpStatusCode.Conflict, "missing"); } return Task.FromResult(new ChatReply("answer", "study", false, null, ReplyPages, 1, 2, 3)); }
    }
    private sealed class MemoryStore(Func<DateTimeOffset> now) : ITemporaryChatStore
    {
        private readonly Dictionary<string, TemporaryChat> _items = []; private readonly Dictionary<string, string> _outside = []; private readonly Dictionary<string, string> _inside = [];
        public Task<TemporaryChat?> GetAsync(string id, CancellationToken ct) => Task.FromResult(GetLive(id));
        public Task<TemporaryChat?> GetInsideAsync(Guid studentId, Guid studySessionId, CancellationToken ct) => Task.FromResult(_inside.TryGetValue($"{studentId:N}:{studySessionId:N}", out string? id) ? GetLive(id) : null);
        public Task<TemporaryChat?> GetOutsideAsync(Guid studentId, CancellationToken ct) => Task.FromResult(_outside.TryGetValue(studentId.ToString(), out string? id) ? GetLive(id) : null);
        public Task<bool> CreateAsync(TemporaryChat chat, CancellationToken ct) { if (chat.Type == TemporaryChatType.Outside && _outside.ContainsKey(chat.StudentId.ToString())) return Task.FromResult(false); if (chat.Type == TemporaryChatType.Inside && _inside.ContainsKey($"{chat.StudentId:N}:{chat.StudySessionId!.Value:N}")) return Task.FromResult(false); _items[chat.ChatId] = chat; if (chat.Type == TemporaryChatType.Outside) _outside[chat.StudentId.ToString()] = chat.ChatId; else _inside[$"{chat.StudentId:N}:{chat.StudySessionId!.Value:N}"] = chat.ChatId; return Task.FromResult(true); }
        public Task<bool> SaveAsync(TemporaryChat chat, CancellationToken ct) { _items[chat.ChatId] = chat; return Task.FromResult(true); }
        public Task DeleteAsync(TemporaryChat chat, CancellationToken ct) { _items.Remove(chat.ChatId); _outside.Remove(chat.StudentId.ToString()); if (chat.StudySessionId is Guid sessionId) _inside.Remove($"{chat.StudentId:N}:{sessionId:N}"); return Task.CompletedTask; }
        public void Seed(TemporaryChat chat) { _items[chat.ChatId] = chat; if (chat.Type == TemporaryChatType.Inside) _inside[$"{chat.StudentId:N}:{chat.StudySessionId!.Value:N}"] = chat.ChatId; }
        public Task<IAsyncDisposable?> TryLockAsync(string resource, CancellationToken ct) => Task.FromResult<IAsyncDisposable?>(new Lock());
        private TemporaryChat? GetLive(string id) { if (!_items.TryGetValue(id, out TemporaryChat? chat)) return null; if (chat.ExpiresAt > now()) return chat; _items.Remove(id); _outside.Remove(chat.StudentId.ToString()); return null; }
        private sealed class Lock : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }
    private sealed class StudentRepository(Student student) : Repository<Student> { public override Task<Student?> FirstOrDefaultAsync(Expression<Func<Student, bool>> criteria, params Expression<Func<Student, object>>[] includes) => Task.FromResult<Student?>(criteria.Compile()(student) ? student : null); }
    private sealed class SessionRepository(StudySession? session) : Repository<StudySession> { public override Task<StudySession?> FirstOrDefaultAsync(Expression<Func<StudySession, bool>> criteria, params Expression<Func<StudySession, object>>[] includes) => Task.FromResult(session is not null && criteria.Compile()(session) ? session : null); }
    private abstract class Repository<T> : IBaseRepository<T> where T : class
    {
        public virtual Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> criteria, params Expression<Func<T, object>>[] includes) => Task.FromResult<T?>(null);
        public Task<T?> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes) => Task.FromResult<T?>(null); public Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes) => Task.FromResult<IEnumerable<T>>([]); public Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> criteria, params Expression<Func<T, object>>[] includes) => Task.FromResult<IEnumerable<T>>([]); public IQueryable<T> GetAll() => Array.Empty<T>().AsQueryable(); public Task<int> CountAsync(Expression<Func<T, bool>> criteria) => Task.FromResult(0); public Task<bool> AnyAsync(Expression<Func<T, bool>> criteria) => Task.FromResult(false); public void Add(T entity) { } public Task AddRangeAsync(IEnumerable<T> entities) => Task.CompletedTask; public void Update(T entity) { } public Task UpdateAsync(T entity) => Task.CompletedTask; public Task DeleteAsync(T entity) => Task.CompletedTask; public void DeleteRange(IEnumerable<T> entities) { } public void Delete(T entity) { }
    }
}
