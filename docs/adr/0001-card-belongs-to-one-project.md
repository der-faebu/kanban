# Card belongs to exactly one Project

The Card Metadata Spec (#31) shipped Card↔Project as many-to-many (`CardProject` join table), letting a card carry several project tags via a pill UI. Introducing billable time tracking (Customer → Project → Card → Time Entry, see [Time Tracking & Calendar Spec](https://github.com/der-faebu/kanban/issues/75)) requires every card's billability to be unambiguous, which a multi-project card can't guarantee. We're tightening Card to a single required `ProjectId`, replacing the `CardProject` many-to-many and its pill-tagging UI.

Status: accepted

Consequences: existing cards tagged with more than one Project need a migration decision (tracked separately — see the "Card→Project Migration Strategy" ticket on the Time Tracking & Calendar Spec map). The multi-project tag UI in `CardDetail.razor` is removed as part of implementing this.
