# Kanban

A Blazor Server kanban board application, expanding into time tracking and lightweight billing/organization structure.

## Language

**Customer**:
An organization or entity that Projects belong to. Every Project has exactly one Customer, including the company's own internal work, which is tracked under a designated "internal" Customer rather than left project-less.
_Avoid_: Client, account.

**Project**:
The unit that time is ultimately reported against. Belongs to exactly one Customer. Carries its own billability (`IsBillable`), independent of which Customer it belongs to — an internal Customer's projects are typically non-billable, but the flag is explicit, not inferred. A Project's name is not unique; the same name may recur across different Customers.
_Avoid_: Board (a Project is not a kanban Board — a Board organizes Cards for workflow, a Project organizes Cards and time for billing/reporting).

**Card** (a.k.a. Work Item):
A unit of work tracked on a kanban Board. Belongs to exactly one Project (tightened from an earlier multi-Project tagging model — see ADR 0001). Time can optionally be logged directly against a Card.

**Time Entry**:
A block of time (`StartAt`/`EndAt`) logged by a user against a Project, optionally against a specific Card within that Project. Card-less entries represent non-board work still tracked for billing/reporting purposes. Implemented as `TimeLogEntry`.
_Avoid_: Time log, time block (used informally for the calendar UI representation, but the domain term is Time Entry).

## Hierarchy

Customer → Project → Card → Time Entry, with Time Entry able to skip Card (but never Project).
