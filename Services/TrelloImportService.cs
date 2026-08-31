using System.Text.Json;
using Kanban.Data;
using Kanban.Data.Dtos;
using Kanban.Data.Dtos.TrelloImport;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface ITrelloImportService
{
    Task<TrelloImportResult> ImportBoardAsync(string userId, Stream jsonStream, string boardName);
}

public class TrelloImportService(IDbContextFactory<ApplicationDbContext> contextFactory) : ITrelloImportService
{
    public async Task<TrelloImportResult> ImportBoardAsync(string userId, Stream jsonStream, string boardName)
    {
        try
        {
            var trelloBoard = await ParseTrelloJsonAsync(jsonStream);
            if (trelloBoard == null)
                return new TrelloImportResult { Success = false, Message = "Invalid Trello JSON format" };

            await using var context = await contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            // Create new board (inlined rather than via IBoardService so it stays on this
            // transaction's own context instead of a separate factory-created one)
            var board = new Board
            {
                Name = boardName,
                Description = trelloBoard.Desc ?? "",
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Boards.Add(board);
            await context.SaveChangesAsync();

            // Create labels map
            var labelMap = new Dictionary<string, Label>();
            foreach (var trelloLabel in trelloBoard.Labels)
            {
                if (!string.IsNullOrEmpty(trelloLabel.Id) && !string.IsNullOrEmpty(trelloLabel.Name))
                {
                    var label = new Label
                    {
                        BoardId = board.Id,
                        Name = trelloLabel.Name,
                        Color = ConvertTrelloColor(trelloLabel.Color),
                        IsDeleted = false
                    };
                    context.Labels.Add(label);
                    labelMap[trelloLabel.Id] = label;
                }
            }
            await context.SaveChangesAsync();

            // Create user email map for assignees
            var users = await context.Users.ToListAsync();
            var userMap = new Dictionary<string, ApplicationUser>();
            foreach (var trelloMember in trelloBoard.Members)
            {
                if (!string.IsNullOrEmpty(trelloMember.Id) && !string.IsNullOrEmpty(trelloMember.Email))
                {
                    var user = users.FirstOrDefault(u => u.Email == trelloMember.Email);
                    if (user != null)
                        userMap[trelloMember.Id] = user;
                }
            }

            // Create lists map
            var listMap = new Dictionary<string, List>();
            foreach (var trelloList in trelloBoard.Lists.OrderBy(l => l.Pos))
            {
                if (!string.IsNullOrEmpty(trelloList.Id) && !string.IsNullOrEmpty(trelloList.Name))
                {
                    var list = new List
                    {
                        BoardId = board.Id,
                        Name = trelloList.Name,
                        Position = trelloList.Pos,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Lists.Add(list);
                    listMap[trelloList.Id] = list;
                }
            }
            await context.SaveChangesAsync();

            // Create cards
            var cardsImported = 0;
            var cardMap = new Dictionary<string, Card>();
            foreach (var trelloCard in trelloBoard.Cards.OrderBy(c => c.Pos))
            {
                if (!string.IsNullOrEmpty(trelloCard.IdList) && listMap.TryGetValue(trelloCard.IdList, out var list))
                {
                    var card = new Card
                    {
                        ListId = list.Id,
                        Title = trelloCard.Name ?? "Untitled",
                        Description = trelloCard.Desc ?? "",
                        Position = trelloCard.Pos,
                        DueDate = trelloCard.Due,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Cards.Add(card);
                    await context.SaveChangesAsync();
                    cardsImported++;
                    if (!string.IsNullOrEmpty(trelloCard.Id))
                        cardMap[trelloCard.Id] = card;

                    // Add assignees
                    foreach (var memberId in trelloCard.IdMembers)
                    {
                        if (userMap.TryGetValue(memberId, out var assignee))
                        {
                            var cardAssignee = new CardAssignee
                            {
                                CardId = card.Id,
                                UserId = assignee.Id,
                                AssignedAt = DateTime.UtcNow
                            };
                            context.CardAssignees.Add(cardAssignee);
                        }
                    }

                    // Add labels
                    foreach (var labelId in trelloCard.IdLabels)
                    {
                        if (labelMap.TryGetValue(labelId, out var label))
                        {
                            var cardLabel = new CardLabel
                            {
                                CardId = card.Id,
                                LabelId = label.Id,
                                AddedAt = DateTime.UtcNow
                            };
                            context.CardLabels.Add(cardLabel);
                        }
                    }
                    await context.SaveChangesAsync();
                }
            }

            // Import comments from "commentCard" actions
            foreach (var action in trelloBoard.Actions)
            {
                if (action.Type != "commentCard" || action.Data?.Card?.Id == null || action.Data.Text == null)
                    continue;

                if (!cardMap.TryGetValue(action.Data.Card.Id, out var card))
                    continue;

                var authorId = userId;
                if (action.MemberCreator?.Id != null && userMap.TryGetValue(action.MemberCreator.Id, out var author))
                    authorId = author.Id;

                context.Comments.Add(new Comment
                {
                    CardId = card.Id,
                    AuthorId = authorId,
                    Text = action.Data.Text,
                    IsDeleted = false,
                    CreatedAt = action.Date ?? DateTime.UtcNow,
                    UpdatedAt = action.Date ?? DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();

            // Import checklist items, flattening Trello's multi-checklist-per-card model into
            // this app's single flat checklist per card, checklist-by-checklist in Trello's order.
            foreach (var cardGroup in trelloBoard.Checklists
                         .Where(cl => !string.IsNullOrEmpty(cl.IdCard) && cardMap.ContainsKey(cl.IdCard))
                         .GroupBy(cl => cl.IdCard!))
            {
                var card = cardMap[cardGroup.Key];
                var position = 0;
                foreach (var checklist in cardGroup.OrderBy(cl => cl.Pos))
                {
                    foreach (var checkItem in checklist.CheckItems.OrderBy(ci => ci.Pos))
                    {
                        if (string.IsNullOrEmpty(checkItem.Name))
                            continue;

                        context.ChecklistItems.Add(new ChecklistItem
                        {
                            CardId = card.Id,
                            Text = checkItem.Name,
                            IsDone = checkItem.State == "complete",
                            Position = position++
                        });
                    }
                }
            }
            await context.SaveChangesAsync();

            await transaction.CommitAsync();

            return new TrelloImportResult
            {
                Success = true,
                Message = $"Board '{boardName}' imported successfully",
                BoardId = board.Id,
                ListsImported = listMap.Count,
                CardsImported = cardsImported,
                LabelsCreated = labelMap.Count
            };
        }
        catch (JsonException ex)
        {
            return new TrelloImportResult { Success = false, Message = $"Invalid JSON format: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new TrelloImportResult { Success = false, Message = $"Import failed: {ex.Message}" };
        }
    }

    private static async Task<TrelloBoard?> ParseTrelloJsonAsync(Stream stream)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return await JsonSerializer.DeserializeAsync<TrelloBoard>(stream, options);
        }
        catch
        {
            return null;
        }
    }

    private static string ConvertTrelloColor(string? trelloColor)
    {
        return trelloColor switch
        {
            "green" => "#28a745",
            "yellow" => "#ffc107",
            "orange" => "#fd7e14",
            "red" => "#dc3545",
            "purple" => "#6f42c1",
            "blue" => "#007bff",
            "sky" => "#17a2b8",
            "lime" => "#32cd32",
            "pink" => "#e91e63",
            "black" => "#212529",
            _ => "#6c757d"
        };
    }
}
