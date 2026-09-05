using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Kanban.Data;
using Kanban.Data.Dtos;
using Kanban.Data.Entities;
using Kanban.Services;
using Kanban.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Kanban.Tests;

public class BoardImportTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private string _userEmail = null!;
    private int _boardId;
    private int _parentCardId;

    public BoardImportTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _userId = Guid.NewGuid().ToString();
        _userEmail = $"user_{_userId}@example.com";
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));
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

        var label = new Label { BoardId = _boardId, Name = "Bug", Color = "#ff0000" };
        dbContext.Labels.Add(label);
        var project = new Project { Name = "Rocket", Color = "#00ff00" };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var parentCard = new Card
        {
            ListId = list.Id,
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
        dbContext.Cards.Add(parentCard);
        await dbContext.SaveChangesAsync();
        _parentCardId = parentCard.Id;

        var childCard = new Card
        {
            ListId = list.Id,
            Title = "Sub-card",
            Position = 1,
            State = CardState.NotStarted,
            ParentCardId = _parentCardId
        };
        dbContext.Cards.Add(childCard);
        await dbContext.SaveChangesAsync();

        dbContext.CardLabels.Add(new CardLabel { CardId = _parentCardId, LabelId = label.Id });
        dbContext.CardProjects.Add(new CardProject { CardId = _parentCardId, ProjectId = project.Id });
        dbContext.CardAssignees.Add(new CardAssignee { CardId = _parentCardId, UserId = _userId });
        dbContext.Comments.Add(new Comment { CardId = _parentCardId, AuthorId = _userId, Text = "A comment" });
        dbContext.ChecklistItems.Add(new ChecklistItem { CardId = _parentCardId, Text = "Step 1", IsDone = true, Position = 0 });
        dbContext.TimeLogEntries.Add(new TimeLogEntry { CardId = _parentCardId, UserId = _userId, Date = DateTime.UtcNow.Date, DurationHours = 2m, Note = "Worked on it" });
        dbContext.CardActivities.Add(new CardActivity { CardId = _parentCardId, UserId = _userId, ActivityType = ActivityType.CardCreated, Metadata = "{}" });
        await dbContext.SaveChangesAsync();

        var attachmentService = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        using var attachmentStream = new MemoryStream(Encoding.UTF8.GetBytes("attachment file contents"));
        await attachmentService.UploadAttachmentAsync(_parentCardId, _userId, attachmentStream, "notes.txt", "text/plain", attachmentStream.Length);
    }

    private async Task<byte[]> ExportZipAsync()
    {
        var response = await _client.GetAsync($"/api/boards/{_boardId}/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static MultipartFormDataContent BuildImportForm(byte[] zipBytes, string boardName, string fileName = "export.zip")
    {
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(new MemoryStream(zipBytes));
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(boardName), "boardName");
        return content;
    }

    [Fact]
    public async Task ImportBoard_RoundTrip_RecreatesFullBoardGraphInANewBoard()
    {
        var zipBytes = await ExportZipAsync();

        using var content = BuildImportForm(zipBytes, "Restored Board");
        var response = await _client.PostAsync("/api/import/board", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BoardImportResult>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(result.BoardId);
        Assert.NotEqual(_boardId, result.BoardId);
        Assert.Equal(1, result.ListsImported);
        Assert.Equal(2, result.CardsImported);
        Assert.Equal(1, result.LabelsCreated);
        Assert.Equal(1, result.ProjectsCreated);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var newBoard = await dbContext.Boards.FirstAsync(b => b.Id == result.BoardId);
        Assert.Equal("Restored Board", newBoard.Name);
        Assert.Equal("A board to export", newBoard.Description);
        Assert.Equal(_userId, newBoard.OwnerId);

        var newList = await dbContext.Lists.SingleAsync(l => l.BoardId == newBoard.Id);
        Assert.Equal("Done", newList.Name);
        Assert.Equal(CardState.Done, newList.AssociatedState);

        var newCards = await dbContext.Cards.Where(c => c.ListId == newList.Id).ToListAsync();
        Assert.Equal(2, newCards.Count);

        var newParent = Assert.Single(newCards, c => c.Title == "Exportable card");
        Assert.NotEqual(_parentCardId, newParent.Id);
        Assert.Equal(CardState.Done, newParent.State);
        Assert.Equal(CardType.Bug, newParent.Type);
        Assert.Equal(CardPriority.High, newParent.Priority);
        Assert.Equal("https://example.com/pr/1", newParent.DevReferenceUrl);
        Assert.Equal("https://example.com/ticket/1", newParent.TicketUrl);
        Assert.Equal(3.5m, newParent.EstimatedHours);
        Assert.Null(newParent.ParentCardId);

        var newChild = Assert.Single(newCards, c => c.Title == "Sub-card");
        Assert.Equal(newParent.Id, newChild.ParentCardId);

        var newLabel = await dbContext.Labels.SingleAsync(l => l.BoardId == newBoard.Id);
        Assert.Equal("Bug", newLabel.Name);
        Assert.True(await dbContext.CardLabels.AnyAsync(cl => cl.CardId == newParent.Id && cl.LabelId == newLabel.Id));

        var newProject = await dbContext.CardProjects
            .Where(cp => cp.CardId == newParent.Id)
            .Select(cp => cp.Project!)
            .SingleAsync();
        Assert.Equal("Rocket", newProject.Name);

        Assert.True(await dbContext.CardAssignees.AnyAsync(ca => ca.CardId == newParent.Id && ca.UserId == _userId));

        var newComment = await dbContext.Comments.SingleAsync(c => c.CardId == newParent.Id);
        Assert.Equal("A comment", newComment.Text);
        Assert.Equal(_userId, newComment.AuthorId);

        var newChecklistItem = await dbContext.ChecklistItems.SingleAsync(i => i.CardId == newParent.Id);
        Assert.Equal("Step 1", newChecklistItem.Text);
        Assert.True(newChecklistItem.IsDone);

        var newTimeEntry = await dbContext.TimeLogEntries.SingleAsync(t => t.CardId == newParent.Id);
        Assert.Equal(2m, newTimeEntry.DurationHours);
        Assert.Equal("Worked on it", newTimeEntry.Note);

        Assert.True(await dbContext.CardActivities.AnyAsync(a => a.CardId == newParent.Id && a.ActivityType == ActivityType.CardCreated));

        var newAttachment = await dbContext.Attachments.SingleAsync(a => a.CardId == newParent.Id);
        Assert.Equal("notes.txt", newAttachment.FileName);
        var absolutePath = Path.Combine(_factory.AttachmentsStorageRoot, newAttachment.StoragePath);
        Assert.True(File.Exists(absolutePath));
        Assert.Equal("attachment file contents", await File.ReadAllTextAsync(absolutePath));
    }

    [Fact]
    public async Task ImportBoard_WithMalformedZip_ReturnsBadRequestAndCreatesNoBoard()
    {
        var boardCountBefore = await CountBoardsAsync();

        using var content = BuildImportForm(Encoding.UTF8.GetBytes("not a zip file"), "Should Not Be Created");
        var response = await _client.PostAsync("/api/import/board", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BoardImportResult>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal(boardCountBefore, await CountBoardsAsync());
    }

    [Fact]
    public async Task ImportBoard_WithMissingFormatVersion_ReturnsBadRequestAndCreatesNoBoard()
    {
        var boardCountBefore = await CountBoardsAsync();

        var zipBytes = BuildZipWithBoardJson("""{"BoardName":"No Version","Lists":[]}""");
        using var content = BuildImportForm(zipBytes, "Should Not Be Created");
        var response = await _client.PostAsync("/api/import/board", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BoardImportResult>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("format-version", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(boardCountBefore, await CountBoardsAsync());
    }

    [Fact]
    public async Task ImportBoard_WithUnsupportedFormatVersion_ReturnsBadRequestAndCreatesNoBoard()
    {
        var boardCountBefore = await CountBoardsAsync();

        var zipBytes = BuildZipWithBoardJson("""{"FormatVersion":99,"BoardName":"Future Export","Lists":[]}""");
        using var content = BuildImportForm(zipBytes, "Should Not Be Created");
        var response = await _client.PostAsync("/api/import/board", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BoardImportResult>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal(boardCountBefore, await CountBoardsAsync());
    }

    private async Task<int> CountBoardsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Boards.CountAsync();
    }

    private static byte[] BuildZipWithBoardJson(string boardJson)
    {
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("board.json", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(boardJson);
        }
        return zipStream.ToArray();
    }

    private string CreateToken(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var key = new UTF8Encoding().GetBytes("test-secret-key-long-enough-for-hs256");
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
