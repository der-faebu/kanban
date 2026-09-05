// Card and list/column dragging via SortableJS (wwwroot/js/vendor/Sortable.min.js, loaded
// globally before this module runs). Replaces native HTML5 DnD for both -- mobile browsers
// don't fire native drag events for touch, SortableJS polyfills pointer/touch dragging itself.
export function initCardSorting(rootElement, dotNetRef) {
    const containers = rootElement.querySelectorAll('.cards-container:not([data-sortable-initialized])');
    containers.forEach((container) => {
        container.dataset.sortableInitialized = 'true';
        Sortable.create(container, {
            group: 'kanban-cards',
            animation: 150,
            ghostClass: 'dragging',
            filter: '[data-sortable-ignore]',
            onEnd: (evt) => {
                const cardId = parseInt(evt.item.dataset.cardId, 10);
                if (Number.isNaN(cardId)) {
                    return;
                }

                const fromListId = parseInt(evt.from.dataset.listId, 10);
                const toListId = parseInt(evt.to.dataset.listId, 10);
                const orderedCardIds = Array.from(evt.to.children)
                    .filter((el) => el.dataset && el.dataset.cardId)
                    .map((el) => parseInt(el.dataset.cardId, 10));

                dotNetRef.invokeMethodAsync('OnCardDropped', cardId, fromListId, toListId, orderedCardIds);
            },
        });
    });
}

// Handle-restricted to '.list-header' so a card drag starting inside a nested .cards-container
// never gets picked up by this (parent) Sortable instance as a column drag.
export function initListSorting(rootElement, dotNetRef) {
    if (rootElement.dataset.sortableInitialized) {
        return;
    }
    rootElement.dataset.sortableInitialized = 'true';

    Sortable.create(rootElement, {
        animation: 150,
        ghostClass: 'dragging',
        handle: '.list-header',
        filter: '[data-sortable-ignore]',
        onEnd: (evt) => {
            const orderedListIds = Array.from(evt.to.children)
                .filter((el) => el.dataset && el.dataset.listItemId)
                .map((el) => parseInt(el.dataset.listItemId, 10));

            dotNetRef.invokeMethodAsync('OnListDropped', orderedListIds);
        },
    });
}
