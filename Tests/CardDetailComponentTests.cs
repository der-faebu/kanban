using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Components.Board;
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
