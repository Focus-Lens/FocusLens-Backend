using System.Text.Json;
using FocusLens.Contracts.Chat;

namespace FocusLens.Api.IntegrationTests;

public sealed class ChatResponseContractTests
{
    [Fact]
    public void Inside_chat_contract_serializes_study_material_without_attachment()
    {
        InsideStudyChatResponse response = new(
            "chat",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(3),
            [],
            new InsideStudyMaterialResponse("lesson.pdf", "application/pdf"));

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.True(json.RootElement.TryGetProperty("studyMaterial", out JsonElement material));
        Assert.Equal("lesson.pdf", material.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", material.GetProperty("contentType").GetString());
        Assert.False(json.RootElement.TryGetProperty("attachment", out _));
    }

    [Fact]
    public void Outside_chat_contract_serializes_attachment_without_study_material()
    {
        OutsideChatResponse response = new(
            "chat",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(3),
            [],
            new ChatAttachmentResponse("attachment.pdf", "application/pdf"));

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.True(json.RootElement.TryGetProperty("attachment", out JsonElement attachment));
        Assert.Equal("attachment.pdf", attachment.GetProperty("fileName").GetString());
        Assert.False(json.RootElement.TryGetProperty("studyMaterial", out _));
    }
}
