namespace Kanban.Data.Entities;

public enum ActivityType
{
    CardCreated,
    CardUpdated,
    CardMoved,
    CardSoftDeleted,
    CardRestored,
    LabelAdded,
    LabelRemoved,
    AssigneeAdded,
    AssigneeRemoved,
    CommentAdded,
    CommentUpdated,
    CommentDeleted,
    ChecklistItemAdded,
    ChecklistItemToggled,
    ChecklistItemDeleted,
    AttachmentAdded,
    AttachmentDeleted,
    PriorityChanged
}
