using FocusLens.Application.StudyChat;
using FocusLens.Contracts.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/chat/sessions")]
[Authorize(Roles = "Student")]
public sealed class ChatController(IStudyChatService chats) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(OutsideChatResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CancellationToken ct) => Ok(ToOutsideResponse(await chats.CreateOutsideAsync(ct)));

    [HttpGet("current")]
    [ProducesResponseType(typeof(OutsideChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Current(CancellationToken ct) { TemporaryChat? chat = await chats.GetOutsideAsync(ct); return chat is null ? NoContent() : Ok(ToOutsideResponse(chat)); }
    [HttpPost("{chatId}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(OutsideChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Attachment(string chatId, [FromForm] IFormFile file, CancellationToken ct)
    { if (file is null) return BadRequest(); await using Stream s = file.OpenReadStream(); using MemoryStream b = new(); await s.CopyToAsync(b, ct); return Ok(ToOutsideResponse(await chats.AddAttachmentAsync(chatId, new(file.FileName, file.ContentType, b.ToArray()), ct))); }
    [HttpDelete("{chatId}/attachments")]
    [ProducesResponseType(typeof(OutsideChatResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAttachment(string chatId, CancellationToken ct) => Ok(ToOutsideResponse(await chats.RemoveAttachmentAsync(chatId, ct)));
    [HttpPost("{chatId}/messages")]
    [ProducesResponseType(typeof(ChatMessageResponseBody), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Message(string chatId, SendChatMessageRequest request, CancellationToken ct) => Ok(ToMessageResponse(await chats.SendOutsideAsync(chatId, request.Message, ct)));
    [HttpDelete("{chatId}")] public async Task<IActionResult> End(string chatId, CancellationToken ct) { await chats.EndOutsideAsync(chatId, ct); return NoContent(); }
    internal static OutsideChatResponse ToOutsideResponse(TemporaryChat c) => new(c.ChatId,c.CreatedAt,c.LastActivityAt,c.ExpiresAt,c.History.Select(ToMessage).ToArray(),c.Attachment is null ? null : new(c.Attachment.FileName,c.Attachment.ContentType));
    internal static ChatMessageResponseBody ToMessageResponse(ChatMessageExchange exchange) => new(ToMessage(exchange.UserMessage), ToMessage(exchange.AssistantMessage));
    internal static ChatMessageResponse ToMessage(TemporaryChatMessage m) => new(m.Id,m.Role,m.Text,m.CreatedAt,m.Category,m.Blocked,m.Reason,m.Pages,m.InputTokens,m.OutputTokens,m.TotalTokens);
}
