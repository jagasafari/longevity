import { useEffect } from 'react'

const LONG_PRESS_MS = 260
const MOVE_TOLERANCE_PX = 8

export type TouchDragHandlers = {
  onDropOnPhoto: (sourceName: string, targetName: string) => void
  onDropIntoGroup: (sourceName: string, targetGroupId: string) => void
}

const photoCard = (el: Element | null): HTMLElement | null =>
  (el as HTMLElement | null)?.closest?.('[data-photo-name]') ?? null

const groupDropTarget = (el: Element | null): HTMLElement | null =>
  (el as HTMLElement | null)?.closest?.('[data-group-id]') ?? null

export function useTouchDrag(handlers: TouchDragHandlers): void {
  useEffect(() => {
    let dragging: string | null = null
    let dragOverEl: HTMLElement | null = null
    let pendingCard: HTMLElement | null = null
    let touchStart: { x: number; y: number } | null = null
    let timer: number | null = null

    const setDragOver = (el: HTMLElement | null): void => {
      if (dragOverEl === el) return
      dragOverEl?.classList.remove('drop-target')
      dragOverEl = el
      dragOverEl?.classList.add('drop-target')
    }

    const clearPending = (): void => {
      if (timer !== null) {
        clearTimeout(timer)
        timer = null
      }
      pendingCard = null
      touchStart = null
    }

    const startDrag = (card: HTMLElement): void => {
      dragging = card.dataset.photoName ?? null
      card.classList.add('dragging')
    }

    const endDrag = (): void => {
      if (dragging) {
        document
          .querySelector(`[data-photo-name="${CSS.escape(dragging)}"]`)
          ?.classList.remove('dragging')
      }
      dragging = null
      setDragOver(null)
    }

    const onTouchStart = (e: TouchEvent): void => {
      const target = e.target as Element | null
      const card = photoCard(target)
      if (!card) return
      const t = e.touches[0]
      if (!t) return
      pendingCard = card
      touchStart = { x: t.clientX, y: t.clientY }
      timer = window.setTimeout(() => {
        if (pendingCard) startDrag(pendingCard)
        clearPending()
      }, LONG_PRESS_MS)
    }

    const onTouchMove = (e: TouchEvent): void => {
      const t = e.touches[0]
      if (!t) return
      if (!dragging && pendingCard && touchStart) {
        if (
          Math.abs(t.clientX - touchStart.x) > MOVE_TOLERANCE_PX ||
          Math.abs(t.clientY - touchStart.y) > MOVE_TOLERANCE_PX
        ) {
          clearPending()
        }
        return
      }
      if (!dragging) return
      e.preventDefault()
      const el = document.elementFromPoint(t.clientX, t.clientY)
      const card = photoCard(el)
      if (card && card.dataset.photoName !== dragging) {
        setDragOver(card)
        return
      }
      const group = groupDropTarget(el)
      setDragOver(group)
    }

    const onTouchEnd = (e: TouchEvent): void => {
      if (!dragging) {
        clearPending()
        return
      }
      const t = e.changedTouches[0]
      if (!t) return endDrag()
      const el = document.elementFromPoint(t.clientX, t.clientY)
      const card = photoCard(el)
      const targetName = card?.dataset.photoName
      const group = groupDropTarget(el)
      const groupId = group?.dataset.groupId

      if (targetName && targetName !== dragging) {
        handlers.onDropOnPhoto(dragging, targetName)
      } else if (groupId) {
        handlers.onDropIntoGroup(dragging, groupId)
      }
      endDrag()
    }

    const onCancel = (): void => {
      clearPending()
      endDrag()
    }

    document.addEventListener('touchstart', onTouchStart, { passive: true })
    document.addEventListener('touchmove', onTouchMove, { passive: false })
    document.addEventListener('touchend', onTouchEnd, { passive: true })
    document.addEventListener('touchcancel', onCancel, { passive: true })

    return () => {
      document.removeEventListener('touchstart', onTouchStart)
      document.removeEventListener('touchmove', onTouchMove)
      document.removeEventListener('touchend', onTouchEnd)
      document.removeEventListener('touchcancel', onCancel)
      clearPending()
      endDrag()
    }
  }, [handlers])
}
