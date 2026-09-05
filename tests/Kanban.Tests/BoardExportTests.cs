using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Kanban.Data;
using Kanban.Data.Dtos.BoardExport;
using Kanban.Data.Entities;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class BoardExportTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private string _userEmail = null!;
    private int _boardId;
    private int _listId;
    private int _cardId;

    public BoardExportTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _userId = Guid.NewGuid().ToString();
        _userEmail = $"user_{_userId}@example.com";
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

        dbContext.Users.Add(new ApplicationUser { Id = _userId, UserName = $"user_{_userId}", Email = _userEmail });
        await dbContext.SaveChangesAsync();

        var board = new Board { Name = "Export Test Board", Description = "A board to export", OwnerId = _userId };
        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();
        _boardId = board.Id;

        var list = new List { BoardId = _boardId, Name = "Done", Position = 0, AssociatedState = CardState.Done };
        dbContext.Lists.Add(list);
        await dbContext.SaveChangesAsync();
        _listId = list.Id;

        var label = new Label { BoardId = _boardId, Name = "Bug", Color = "#ff0000" };
        dbContext.Labels.Add(label);
        var project = new Project { Name = "Rocket", Color = "#00ff00" };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var card = new Card
        {
            ListId = _listId,
            Title = "Exportable card",
            Description = "Has everything",
            Position = 0,
            State = CardState.Done,
            Type = CardType.Bug,
            Priority = CardPriority.High,
            DevReferenceUrl = "https://example.com/pr/1",
            TicketUrl = "https://example.com/ticket/1",
            EstimatedHours = 3.5m
        };
        dbContext.Cards.Add(card);
        await dbContext.SaveChangesAsync();
        _cardId = card.Id;

        dbContext.CardLabels.Add(new CardLabel { CardId = _cardId, LabelId = label.Id });
        dbContext.CardProjects.Add(new CardProject { CardId = _cardId, ProjectId = project.Id });
        dbContext.CardAssignees.Add(new CardAssignee { CardId = _cardId, UserId = _userId });
        dbContext.Comments.Add(new Comment { CardId = _cardId, AuthorId = _userId, Text = "A comment" });
        dbContext.ChecklistItems.Add(new ChecklistItem { CardId = _cardId, Text = "Step 1", IsDone = true, Position = 0 });
        dbContext.TimeLogEntries.Add(new TimeLogEntry { CardId = _cardId, UserId = _userId, Date = DateTime.UtcNow.Date, DurationHours = 2m, Note = "Worked on it" });
        dbContext.CardActivities.Add(new CardActivity { CardId = _cardId, UserId = _userId, ActivityType = ActivityType.CardCreated, Metadata = "{}" });
        await dbContext.SaveChangesAsync();

        var attachmentService = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        using var attachmentStream = new MemoryStream(Encoding.UTF8.GetBytes("attachment file contents"));
        await attachmentService.UploadAttachmentAsync(_cardId, _userId, attachmentStream, "notes.txt", "text/plain", attachmentStream.Length);
    }

    [Fact]
    public async Task ExportBoard_AsOwner_ReturnsZipWithBoardJsonAndAttachment()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.GetAsync($"/api/boards/{_boardId}/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        var zipBytes = await response.Content.ReadAsByteArrayAsync();
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var boardJsonEntry = archive.GetEntry("board.json");
        Assert.NotNull(boardJsonEntry);

        using var boardJsonStream = boardJsonEntry!.Open();
        var export = await JsonSerializer.DeserializeAsync<BoardExportFile>(boardJsonStream);
        Assert.NotNull(export);
        Assert.Equal(1, export!.FormatVersion);
        Assert.Equal("Export Test Board", export.BoardName);
        Assert.Equal("A board to export", export.BoardDescription);

        var list = Assert.Single(export.Lists);
        Assert.Equal("Done", list.Name);
        Assert.Equal(CardState.Done, list.AssociatedState);

        var card = Assert.Single(list.Cards);
        Assert.Equal("Exportable card", card.Title);
        Assert.Equal(CardState.Done, card.State);
        Assert.Equal(CardType.Bug, card.Type);
        Assert.Equal(CardPriority.High, card.Priority);
        Assert.Equal("https://example.com/pr/1", card.DevReferenceUrl);
        Assert.Equal("https://example.com/ticket/1", card.TicketUrl);
        Assert.Equal(3.5m, card.EstimatedHours);
        Assert.Single(card.LabelIds);
        Assert.Single(card.ProjectIds);
        Assert.Contains(_userEmail, card.AssigneeEmails);
        Assert.Single(card.Comments);
        Assert.Equal("A comment", card.Comments[0].Text);
        Assert.Single(card.ChecklistItems);
        Assert.True(card.ChecklistItems[0].IsDone);
        Assert.Single(card.TimeLogEntries);
        Assert.Equal(2m, card.TimeLogEntries[0].DurationHours);
        Assert.NotEmpty(card.Activities);

        var attachment = Assert.Single(card.Attachments);
        Assert.Equal("notes.txt", attachment.FileName);
        var attachmentEntry = archive.GetEntry(attachment.ZipEntryPath);
        Assert.NotNull(attachmentEntry);

        using var attachmentEntryStream = attachmentEntry!.Open();
        using var reader = new StreamReader(attachmentEntryStream);
        var contents = await reader.ReadToEndAsync();
        Assert.Equal("attachment file contents", contents);
    }

    [Fact]
    public async Task ExportBoard_AsNonOwnerMember_IsForbidden()
    {
        var otherUserId = Guid.NewGuid().ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.Add(new ApplicationUser { Id = otherUserId, UserName = $"user_{otherUserId}", Email = $"user_{otherUserId}@example.com" });
            dbContext.BoardMembers.Add(new BoardMember { BoardId = _boardId, UserId = otherUserId, Role = BoardMemberRole.Member });
            await dbContext.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var response = await _client.GetAsync($"/api/boards/{_boardId}/export");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExportBoard_ForNonExistentBoard_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.GetAsync($"/api/boards/999999/export");
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
