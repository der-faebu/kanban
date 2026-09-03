using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Components.Board;
using Kanban.Data;
using Kanban.Data.Entities;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class CardDetailComponentTests : CardDetailTestContext
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Render_ShowsSeededCardTitleAndIdCode()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        Assert.Contains(card.Title, component.Markup);
        Assert.Contains(CardVisualStyles.IdCode(board.Name, card.Id), component.Markup);
    }

    [Fact]
    public async Task Render_ShowsLeftColumnContent()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        Assert.Contains("Detailed description here", component.Markup);
        Assert.Contains("First checklist item", component.Markup);
    }

    [Fact]
    public async Task SwitchingToActivityTab_ShowsActivityInsteadOfDiscussion()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        // Discussion is the default active tab.
        Assert.Contains("Write a comment", component.Markup);

        await component.InvokeAsync(() =>
            component.FindAll(".mud-tab").First(e => e.TextContent.Trim() == "Activity").Click());

        component.WaitForState(() => component.Markup.Contains("created this card"), WaitTimeout);
        Assert.Contains("created this card", component.Markup);
    }

    [Fact]
    public async Task PostComment_ViaDiscussionTab_PersistsAndAppearsInList()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        await component.InvokeAsync(() => component.Find("textarea").Change("Looks good to me"));
        await component.InvokeAsync(() =>
            component.FindAll("button").First(b => b.TextContent.Trim() == "Post comment").Click());

        // Match the rendered comment element specifically -- "comment-text" alone also matches
        // the component's own scoped <style> rule and would be a false positive.
        component.WaitForState(() => component.Markup.Contains("class=\"comment-text\">Looks good to me"), WaitTimeout);

        var commentService = Services.GetRequiredService<ICommentService>();
        var comments = await commentService.GetCardCommentsAsync(card.Id, UserId);
        Assert.Contains(comments, c => c.Text == "Looks good to me");
    }

    [Fact]
    public async Task Toolbar_ReflectsSeededStatePriorityTypeDueDate()
    {
        var (board, card) = await SeedCardWithContentAsync();
        var cardService = Services.GetRequiredService<ICardService>();
        await cardService.SetCardStateAsync(card.Id, UserId, CardState.InProgress);
        await cardService.SetCardPriorityAsync(card.Id, UserId, CardPriority.High);
        await cardService.SetCardTypeAsync(card.Id, UserId, CardType.Bug);
        var dueDate = DateTime.SpecifyKind(new DateTime(2026, 12, 25), DateTimeKind.Utc);
        await cardService.UpdateCardAsync(card.Id, UserId, card.Title, card.Description, dueDate);

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(CardStateStyles.Label(CardState.InProgress)), WaitTimeout);

        Assert.Contains(CardPriorityStyles.Label(CardPriority.High), component.Markup);
        Assert.Contains(CardTypeStyles.Label(CardType.Bug), component.Markup);
        Assert.Contains(dueDate.ToShortDateString(), component.Markup);
    }

    [Fact]
    public async Task ChangingState_ViaPopover_UpdatesPillAndPersists()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        await component.InvokeAsync(() => component.Find(".state-menu button").Click());
        await component.InvokeAsync(() =>
            PopoverProvider.FindAll(".mud-menu-item").First(e => e.TextContent.Trim() == "Done").Click());

        component.WaitForState(() => component.Markup.Contains(CardStateStyles.Label(CardState.Done)), WaitTimeout);

        // The click's InvokeAsync awaits the dispatched handler, but the resulting DB write can
        // still land a beat after the UI reflects the new value -- poll instead of a single read.
        var cardService = Services.GetRequiredService<ICardService>();
        component.WaitForAssertion(() =>
            Assert.Equal(CardState.Done, cardService.GetCardByIdAsync(card.Id, UserId).GetAwaiter().GetResult()!.State), WaitTimeout);
    }

    [Fact]
    public async Task ClearingPriority_ViaPopover_PersistsClear()
    {
        var (board, card) = await SeedCardWithContentAsync();
        var cardService = Services.GetRequiredService<ICardService>();
        await cardService.SetCardPriorityAsync(card.Id, UserId, CardPriority.High);

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(CardPriorityStyles.Label(CardPriority.High)), WaitTimeout);

        await component.InvokeAsync(() =>
            component.FindAll(".toolbar-item button").First(b => b.TextContent.Trim() == CardPriorityStyles.Label(CardPriority.High)).Click());
        await component.InvokeAsync(() =>
            PopoverProvider.FindAll(".mud-menu-item").First(e => e.TextContent.Trim() == "Clear").Click());

        // The click's InvokeAsync awaits the dispatched handler, but the resulting DB write can
        // still land a beat after the UI reflects the cleared value -- poll instead of a single read.
        component.WaitForAssertion(() =>
            Assert.Null(cardService.GetCardByIdAsync(card.Id, UserId).GetAwaiter().GetResult()!.Priority), WaitTimeout);
    }

    [Fact]
    public async Task ClearingType_ViaPopover_PersistsClear()
    {
        var (board, card) = await SeedCardWithContentAsync();
        var cardService = Services.GetRequiredService<ICardService>();
        await cardService.SetCardTypeAsync(card.Id, UserId, CardType.Bug);

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(CardTypeStyles.Label(CardType.Bug)), WaitTimeout);

        await component.InvokeAsync(() =>
            component.FindAll(".toolbar-item button").First(b => b.TextContent.Trim() == CardTypeStyles.Label(CardType.Bug)).Click());
        await component.InvokeAsync(() =>
            PopoverProvider.FindAll(".mud-menu-item").First(e => e.TextContent.Trim() == "Clear").Click());

        component.WaitForAssertion(() =>
            Assert.Null(cardService.GetCardByIdAsync(card.Id, UserId).GetAwaiter().GetResult()!.Type), WaitTimeout);
    }

    [Fact]
    public async Task AddAndRemoveLabel_ViaPopover_UpdatesSummaryAndPersists()
    {
        var (board, card) = await SeedCardWithContentAsync();
        var labelService = Services.GetRequiredService<ILabelService>();
        var label = await labelService.CreateLabelAsync(board.Id, UserId, "urgent-fix", "#ff0000");

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        await component.InvokeAsync(() =>
            component.FindAll(".toolbar-item button").First(b => b.TextContent.Trim() == "Label").Click());
        await component.InvokeAsync(() =>
            PopoverProvider.FindAll(".mud-chip").First(c => c.TextContent.Contains(label.Name)).Click());

        component.WaitForState(() => component.FindAll(".toolbar-item button").Any(b => b.TextContent.Trim() == "Label (1)"), WaitTimeout);

        var cardService = Services.GetRequiredService<ICardService>();
        var afterAdd = await cardService.GetCardLabelsAsync(card.Id, UserId);
        Assert.Contains(afterAdd, l => l.Id == label.Id);

        await component.InvokeAsync(() => PopoverProvider.Find(".pill-remove").Click());
        component.WaitForState(() => component.FindAll(".toolbar-item button").Any(b => b.TextContent.Trim() == "Label"), WaitTimeout);

        var afterRemove = await cardService.GetCardLabelsAsync(card.Id, UserId);
        Assert.DoesNotContain(afterRemove, l => l.Id == label.Id);
    }

    [Fact]
    public async Task AddAndRemoveProject_ViaPopover_UpdatesSummaryAndPersists()
    {
        var (board, card) = await SeedCardWithContentAsync();
        var projectService = Services.GetRequiredService<IProjectService>();
        var project = await projectService.CreateProjectAsync(null, UserId, "Platform Migration", "#00ff00");

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        await component.InvokeAsync(() =>
            component.FindAll(".toolbar-item button").First(b => b.TextContent.Trim() == "Project").Click());
        await component.InvokeAsync(() =>
            PopoverProvider.FindAll(".mud-chip").First(c => c.TextContent.Contains(project.Name)).Click());

        component.WaitForState(() => component.FindAll(".toolbar-item button").Any(b => b.TextContent.Trim() == "Project (1)"), WaitTimeout);

        var cardService = Services.GetRequiredService<ICardService>();
        var afterAdd = await cardService.GetCardProjectsAsync(card.Id, UserId);
        Assert.Contains(afterAdd, p => p.Id == project.Id);

        await component.InvokeAsync(() => PopoverProvider.Find(".pill-remove").Click());
        component.WaitForState(() => component.FindAll(".toolbar-item button").Any(b => b.TextContent.Trim() == "Project"), WaitTimeout);

        var afterRemove = await cardService.GetCardProjectsAsync(card.Id, UserId);
        Assert.DoesNotContain(afterRemove, p => p.Id == project.Id);
    }

    [Fact]
    public async Task Toolbar_ReflectsSeededAssignee()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var dbContext = DbContext;
        var otherUserId = Guid.NewGuid().ToString();
        dbContext.Users.Add(new ApplicationUser { Id = otherUserId, UserName = $"member_{otherUserId}", Email = $"member_{otherUserId}@example.com" });
        await dbContext.SaveChangesAsync();
        var boardService = Services.GetRequiredService<IBoardService>();
        await boardService.AddBoardMemberAsync(board.Id, UserId, otherUserId, BoardMemberRole.Member);
        var cardService = Services.GetRequiredService<ICardService>();
        await cardService.AddAssigneeAsync(card.Id, UserId, otherUserId);

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);

        component.WaitForState(() => component.FindAll(".toolbar-item button").Any(b => b.TextContent.Trim() == "Assigned (1)"), WaitTimeout);
    }

    [Fact]
    public async Task RemoveAssignee_ViaPopover_UpdatesSummaryAndPersists()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var dbContext = DbContext;
        var otherUserId = Guid.NewGuid().ToString();
        dbContext.Users.Add(new ApplicationUser { Id = otherUserId, UserName = $"member_{otherUserId}", Email = $"member_{otherUserId}@example.com" });
        await dbContext.SaveChangesAsync();
        var boardService = Services.GetRequiredService<IBoardService>();
        await boardService.AddBoardMemberAsync(board.Id, UserId, otherUserId, BoardMemberRole.Member);
        var cardService = Services.GetRequiredService<ICardService>();
        await cardService.AddAssigneeAsync(card.Id, UserId, otherUserId);

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.FindAll(".toolbar-item button").Any(b => b.TextContent.Trim() == "Assigned (1)"), WaitTimeout);

        await component.InvokeAsync(() =>
            component.FindAll(".toolbar-item button").First(b => b.TextContent.Trim() == "Assigned (1)").Click());
        await component.InvokeAsync(() => PopoverProvider.Find(".pill-remove").Click());

        component.WaitForAssertion(() =>
            Assert.DoesNotContain(cardService.GetCardAssigneesAsync(card.Id, UserId).GetAwaiter().GetResult(), a => a.Id == otherUserId), WaitTimeout);
    }

    [Fact]
    public async Task SaveDevReferenceUrl_ViaInputRow_Persists()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        var devLinkRow = component.FindAll(".link-row")
            .First(row => row.QuerySelector("input")?.GetAttribute("placeholder") == "https://github.com/org/repo/pull/1");

        await component.InvokeAsync(() => devLinkRow.QuerySelector("input")!.Change("https://github.com/org/repo/pull/42"));
        await component.InvokeAsync(() => devLinkRow.QuerySelector("button")!.Click());

        var cardService = Services.GetRequiredService<ICardService>();
        component.WaitForAssertion(() =>
            Assert.Equal("https://github.com/org/repo/pull/42", cardService.GetCardByIdAsync(card.Id, UserId).GetAwaiter().GetResult()!.DevReferenceUrl), WaitTimeout);
    }

    [Fact]
    public async Task SaveTicketUrl_ViaInputRow_Persists()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        var ticketLinkRow = component.FindAll(".link-row")
            .First(row => row.QuerySelector("input")?.GetAttribute("placeholder") == "https://example.atlassian.net/browse/PROJ-1");

        await component.InvokeAsync(() => ticketLinkRow.QuerySelector("input")!.Change("https://example.atlassian.net/browse/PROJ-9"));
        await component.InvokeAsync(() => ticketLinkRow.QuerySelector("button")!.Click());

        var cardService = Services.GetRequiredService<ICardService>();
        component.WaitForAssertion(() =>
            Assert.Equal("https://example.atlassian.net/browse/PROJ-9", cardService.GetCardByIdAsync(card.Id, UserId).GetAwaiter().GetResult()!.TicketUrl), WaitTimeout);
    }

    [Fact]
    public async Task UploadAttachment_ViaUploadButton_CreatesAttachment()
    {
        var (board, card) = await SeedCardWithContentAsync();

        var component = await RenderCardDetailAsync(board.Id, card.Id, board.Name);
        component.WaitForState(() => component.Markup.Contains(card.Title), WaitTimeout);

        var inputFile = component.FindComponent<InputFile>();
        await component.InvokeAsync(() =>
            inputFile.UploadFiles(InputFileContent.CreateFromText("hello world", "notes.txt")));

        var attachmentService = Services.GetRequiredService<IAttachmentService>();
        component.WaitForAssertion(() =>
            Assert.Contains(attachmentService.GetAttachmentsAsync(card.Id, UserId).GetAwaiter().GetResult(), a => a.FileName == "notes.txt"), WaitTimeout);
    }

    private async Task<(Board Board, Card Card)> SeedCardWithContentAsync()
    {
        var dbContext = DbContext;

        var board = new Board { Name = "Test Board", OwnerId = UserId };
        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();

        var list = new List { BoardId = board.Id, Name = "Test List", Position = 0 };
        dbContext.Lists.Add(list);
        await dbContext.SaveChangesAsync();

        var cardService = Services.GetRequiredService<ICardService>();
        var card = await cardService.CreateCardAsync(list.Id, UserId, "Ship the redesign", "Detailed description here");

        var checklistService = Services.GetRequiredService<IChecklistService>();
        await checklistService.AddItemAsync(card.Id, UserId, "First checklist item");

        return (board, card);
    }
}
