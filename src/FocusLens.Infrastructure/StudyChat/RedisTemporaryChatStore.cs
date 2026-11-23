using System.Text.Json;
using FocusLens.Application.StudyChat;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FocusLens.Infrastructure.StudyChat;

public sealed class RedisTemporaryChatStore(IConnectionMultiplexer redis, IOptions<ChatOptions> options) : ITemporaryChatStore
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(Math.Max(1, options.Value.InactivityTtlSeconds));
    private static string ChatKey(string id) => $"focuslens:chat:{id}";
    private static string InsideKey(Guid student, Guid session) => $"focuslens:chat:inside:{student:N}:{session:N}";
    private static string OutsideKey(Guid student) => $"focuslens:chat:outside:{student:N}";
    public async Task<TemporaryChat?> GetAsync(string id, CancellationToken ct) => Deserialize(await _db.StringGetAsync(ChatKey(id)));
    public async Task<TemporaryChat?> GetInsideAsync(Guid student, Guid session, CancellationToken ct) => await GetMappedAsync(InsideKey(student, session), ct);
    public async Task<TemporaryChat?> GetOutsideAsync(Guid student, CancellationToken ct) => await GetMappedAsync(OutsideKey(student), ct);
    private async Task<TemporaryChat?> GetMappedAsync(string map, CancellationToken ct) { RedisValue id = await _db.StringGetAsync(map); return id.IsNullOrEmpty ? null : await GetAsync(id.ToString(), ct); }
    public async Task<bool> CreateAsync(TemporaryChat chat, CancellationToken ct)
    {
        string map = chat.Type == TemporaryChatType.Inside ? InsideKey(chat.StudentId, chat.StudySessionId!.Value) : OutsideKey(chat.StudentId);
        if (!await _db.StringSetAsync(map, chat.ChatId, _ttl, When.NotExists)) return false;
        if (await _db.StringSetAsync(ChatKey(chat.ChatId), JsonSerializer.Serialize(chat), _ttl, When.NotExists)) return true;
        await _db.KeyDeleteAsync(map); return false;
    }
    public async Task<bool> SaveAsync(TemporaryChat chat, CancellationToken ct)
    {
        string map = chat.Type == TemporaryChatType.Inside ? InsideKey(chat.StudentId, chat.StudySessionId!.Value) : OutsideKey(chat.StudentId);
        var batch = _db.CreateBatch();
        Task a = batch.StringSetAsync(ChatKey(chat.ChatId), JsonSerializer.Serialize(chat), _ttl);
        Task b = batch.StringSetAsync(map, chat.ChatId, _ttl);
        batch.Execute(); await Task.WhenAll(a,b); return true;
    }
    public async Task DeleteAsync(TemporaryChat chat, CancellationToken ct)
    { string map = chat.Type == TemporaryChatType.Inside ? InsideKey(chat.StudentId, chat.StudySessionId!.Value) : OutsideKey(chat.StudentId); await _db.KeyDeleteAsync([ChatKey(chat.ChatId), map]); }
    public async Task<IAsyncDisposable?> TryLockAsync(string resource, CancellationToken ct)
    { string key = $"focuslens:chat:lock:{resource}"; RedisValue token = Guid.NewGuid().ToString("N"); if (!await _db.StringSetAsync(key, token, TimeSpan.FromSeconds(60), When.NotExists)) return null; return new RedisLock(_db,key,token); }
    private static TemporaryChat? Deserialize(RedisValue value) => value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<TemporaryChat>(value.ToString());
    private sealed class RedisLock(IDatabase db, string key, RedisValue token) : IAsyncDisposable { private const string Script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end"; public async ValueTask DisposeAsync() => await db.ScriptEvaluateAsync(Script, [key], [token]); }
}
