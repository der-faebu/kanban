using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Kanban.Data;
using Kanban.Data.Entities;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class AttachmentTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listId;
    private int _cardId;

    public AttachmentTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _userId = Guid.NewGuid().ToString();
        await SeedTestData();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser { Id = _userId, UserName = $"user_{_userId}", Email = $"user_{_userId}@example.com" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var board = new Board { Name = "Test Board", OwnerId = _userId };
        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();
        _boardId = board.Id;

        var list = new List { BoardId = _boardId, Name = "Test List", Position = 0 };
        dbContext.Lists.Add(list);
        await dbContext.SaveChangesAsync();
        _listId = list.Id;

        var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
        dbContext.Cards.Add(card);
        await dbContext.SaveChangesAsync();
        _cardId = card.Id;
    }

    private async Task<string> CreateOtherUserAsync()
    {
        var otherUserId = Guid.NewGuid().ToString();
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var otherUser = new ApplicationUser { Id = otherUserId, UserName = $"user_{otherUserId}", Email = $"user_{otherUserId}@example.com" };
        dbContext.Users.Add(otherUser);
        await dbContext.SaveChangesAsync();
        return otherUserId;
    }

    private void AuthenticateAs(string userId)
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(userId));
    }

    private static MultipartFormDataContent BuildUpload(byte[] bytes, string fileName = "test.txt", string contentType = "text/plain")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task UploadAttachment_WithinSizeLimit_CreatesRowAndFile()
    {
        AuthenticateAs(_userId);
        using var content = BuildUpload("hello world"u8.ToArray());

        var response = await _client.PostAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attachment = await response.Content.ReadFromJsonAsync<Attachment>();
        Assert.NotNull(attachment);
        Assert.Equal("test.txt", attachment!.FileName);

        var absolutePath = Path.Combine(_factory.AttachmentsStorageRoot, attachment.StoragePath);
        Assert.True(File.Exists(absolutePath));
    }

    [Fact]
    public async Task UploadAttachment_ExceedingSizeLimit_ReturnsBadRequestAndLeavesNoFile()
    {
        AuthenticateAs(_userId);
        var oversized = new byte[2 * 1024 * 1024]; // 2MB, over the 1MB test limit
        using var content = BuildUpload(oversized);

        var response = await _client.PostAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var cardUploadDir = Path.Combine(_factory.AttachmentsStorageRoot, _cardId.ToString());
        Assert.False(Directory.Exists(cardUploadDir) && Directory.EnumerateFiles(cardUploadDir).Any());
    }

    [Fact]
    public async Task UploadAttachment_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);
        using var content = BuildUpload("hello"u8.ToArray());

        var response = await _client.PostAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAttachments_ReturnsMetadata()
    {
        AuthenticateAs(_userId);
        using var uploadContent = BuildUpload("hello"u8.ToArray());
        await _client.PostAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments", uploadContent);

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attachments = await response.Content.ReadFromJsonAsync<List<Attachment>>();
        Assert.NotNull(attachments);
        Assert.Single(attachments!);
        Assert.Equal(_userId, attachments![0].UploadedByUserId);
    }

    [Fact]
    public async Task GetAttachments_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAttachment_ReturnsCorrectBytesAndContentType()
    {
        AuthenticateAs(_userId);
        var bytes = "hello world"u8.ToArray();
        using var uploadContent = BuildUpload(bytes, "greeting.txt", "text/plain");
        var uploadResponse = await _client.PostAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments", uploadContent);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments/{attachment!.Id}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        var downloadedBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes, downloadedBytes);
    }

    [Fact]
    public async Task DeleteAttachment_RemovesRowAndFile()
    {
        AuthenticateAs(_userId);
        using var uploadContent = BuildUpload("hello"u8.ToArray());
        var uploadResponse = await _client.PostAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments", uploadContent);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();
        var absolutePath = Path.Combine(_factory.AttachmentsStorageRoot, attachment!.StoragePath);
        Assert.True(File.Exists(absolutePath));

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments/{attachment.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(File.Exists(absolutePath));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null(await dbContext.Attachments.FindAsync(attachment.Id));
    }

    [Fact]
    public async Task DeleteAttachment_AsNonBoardMember_IsRejected()
    {
        AuthenticateAs(_userId);
        using var uploadContent = BuildUpload("hello"u8.ToArray());
        var uploadResponse = await _client.PostAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments", uploadContent);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();

        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments/{attachment!.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAttachment_AsNonBoardMember_IsRejected()
    {
        AuthenticateAs(_userId);
        using var uploadContent = BuildUpload("hello"u8.ToArray());
        var uploadResponse = await _client.PostAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments", uploadContent);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();

        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/attachments/{attachment!.Id}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private string CreateToken(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var key = new System.Text.UTF8Encoding().GetBytes("test-secret-key-long-enough-for-hs256");
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
