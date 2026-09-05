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

// Desktop-only click-drag panning on the board's empty background. Ignores any mousedown that
// starts inside a '.list' (card/column drag territory, owned by SortableJS above) or a
// '[data-sortable-ignore]' element (e.g. AddListForm), so panning and dragging never fight over
// the same gesture. Mobile is unaffected -- it already pans via native touch scrolling and never
// fires mouse events.
//
// mousemove/mouseup are bound on document (not rootElement) so a drag keeps tracking even if the
// pointer leaves the container mid-gesture. That means they must be explicitly torn down via
// disposeBoardPanning -- a fresh rootElement on every board navigation would otherwise leave the
// old listeners (and their closed-over element reference) attached to document forever.
export function initBoardPanning(rootElement) {
    if (rootElement.dataset.panningInitialized) {
        return;
    }
    rootElement.dataset.panningInitialized = 'true';

    let isPanning = false;
    let startX = 0;
    let startScrollLeft = 0;

    const onMouseDown = (evt) => {
        if (evt.button !== 0 || evt.target.closest('.list') || evt.target.closest('[data-sortable-ignore]')) {
            return;
        }

        isPanning = true;
        startX = evt.pageX;
        startScrollLeft = rootElement.scrollLeft;
        rootElement.classList.add('panning');
        evt.preventDefault();
    };

    const onMouseMove = (evt) => {
        if (!isPanning) {
            return;
        }
        rootElement.scrollLeft = startScrollLeft - (evt.pageX - startX);
    };

    const onMouseUp = () => {
        if (!isPanning) {
            return;
        }
        isPanning = false;
        rootElement.classList.remove('panning');
    };

    rootElement.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);

    rootElement._disposePanning = () => {
        rootElement.removeEventListener('mousedown', onMouseDown);
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
    };
}

export function disposeBoardPanning(rootElement) {
    rootElement?._disposePanning?.();
}
