let dragging = null;
let dragOverCard = null;
let dotNetRef = null;
let pendingCard = null;
let touchStart = null;
let longPressTimer = null;

const LONG_PRESS_MS = 260;
const MOVE_TOLERANCE_PX = 8;

function clearPending() {
    if (longPressTimer) {
        clearTimeout(longPressTimer);
        longPressTimer = null;
    }
    pendingCard = null;
    touchStart = null;
}

function startDrag(card) {
    dragging = card.dataset.photoName;
    card.classList.add('touch-dragging');
}

function endDrag() {
    if (dragging) {
        const sourceCard = document.querySelector(
            `[data-photo-name="${CSS.escape(dragging)}"]`
        );
        sourceCard?.classList.remove('touch-dragging');
    }
    dragging = null;
    setDragOver(null);
}

function photoCard(el) {
    return el?.closest?.('[data-photo-name]') ?? null;
}

function groupDropTarget(el) {
    return el?.closest?.('[data-group-id]') ?? null;
}

function setDragOver(card) {
    if (dragOverCard === card) return;
    dragOverCard?.classList.remove('touch-drag-over');
    dragOverCard = card;
    dragOverCard?.classList.add('touch-drag-over');
}

function onTouchStart(e) {
    const card = photoCard(e.target);
    if (!card) return;

    const touch = e.touches[0];
    pendingCard = card;
    touchStart = { x: touch.clientX, y: touch.clientY };
    longPressTimer = setTimeout(() => {
        if (!pendingCard) return;
        startDrag(pendingCard);
        clearPending();
    }, LONG_PRESS_MS);
}

function onTouchMove(e) {
    const touch = e.touches[0];

    if (!dragging && pendingCard && touchStart) {
        const dx = Math.abs(touch.clientX - touchStart.x);
        const dy = Math.abs(touch.clientY - touchStart.y);

        // If the user starts moving before long-press, treat it as scroll.
        if (dx > MOVE_TOLERANCE_PX || dy > MOVE_TOLERANCE_PX) {
            clearPending();
        }
        return;
    }

    if (!dragging) return;
    e.preventDefault();
    const el = document.elementFromPoint(touch.clientX, touch.clientY);
    const card = photoCard(el);
    if (card && card?.dataset.photoName !== dragging) {
        setDragOver(card);
        return;
    }

    const group = groupDropTarget(el);
    if (group) {
        setDragOver(group);
        return;
    }

    setDragOver(null);
}

function onTouchEnd(e) {
    if (!dragging) {
        clearPending();
        return;
    }

    if (!dragging) return;
    const touch = e.changedTouches[0];

    const el = document.elementFromPoint(touch.clientX, touch.clientY);
    const targetCard = photoCard(el);
    const targetName = targetCard?.dataset.photoName;
    const targetGroup = groupDropTarget(el);
    const targetGroupId = targetGroup?.dataset.groupId;

    if (targetName && targetName !== dragging) {
        dotNetRef?.invokeMethodAsync('DropOnFromTouch', dragging, targetName)
            ?.catch(() => {});
    } else if (targetGroupId) {
        dotNetRef?.invokeMethodAsync('DropIntoGroupFromTouch', dragging, targetGroupId)
            ?.catch(() => {});
    }
    endDrag();
}

function onTouchCancel() {
    clearPending();
    endDrag();
}

export function init(ref) {
    dotNetRef = ref;
    document.addEventListener('touchstart', onTouchStart, { passive: true });
    document.addEventListener('touchmove', onTouchMove, { passive: false });
    document.addEventListener('touchend', onTouchEnd, { passive: true });
    document.addEventListener('touchcancel', onTouchCancel, { passive: true });
}

export function dispose() {
    document.removeEventListener('touchstart', onTouchStart);
    document.removeEventListener('touchmove', onTouchMove);
    document.removeEventListener('touchend', onTouchEnd);
    document.removeEventListener('touchcancel', onTouchCancel);
    clearPending();
    endDrag();
    dotNetRef = null;
}
