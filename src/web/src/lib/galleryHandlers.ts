import { useMemo } from 'react'
import {
  useDeletePhoto,
  useGroupPhotos,
  useMoveToGroup,
  useUngroup,
} from '../api/hooks'
import { useUi } from '../store/ui'
import { useTouchDrag } from './touchDrag'
import type { GroupSectionHandlers } from '../components/GroupSection'

export function useGalleryHandlers(): GroupSectionHandlers {
  const ui = useUi()
  const groupMut = useGroupPhotos()
  const moveMut = useMoveToGroup()
  const deleteMut = useDeletePhoto()
  const ungroupMut = useUngroup()

  useTouchDrag(
    useMemo(
      () => ({
        onDropOnPhoto: (s, t) =>
          groupMut.mutate({ sourceName: s, targetName: t }),
        onDropIntoGroup: (s, g) =>
          moveMut.mutate({ photoName: s, targetGroupId: g }),
      }),
      [groupMut, moveMut],
    ),
  )

  return {
    onStartDrag: (name) => ui.startDrag(name),
    onEndDrag: () => ui.startDrag(null),
    onDropOnPhoto: (targetName) => {
      const source = ui.draggedPhotoName
      ui.startDrag(null)
      if (!source || source === targetName) return
      groupMut.mutate({ sourceName: source, targetName })
    },
    onDropIntoGroup: (groupId) => {
      const source = ui.draggedPhotoName
      ui.startDrag(null)
      if (!source) return
      moveMut.mutate({ photoName: source, targetGroupId: groupId })
    },
    onOpenLightbox: (p, scope) => {
      if (ui.draggedPhotoName) return
      const idx = Math.max(0, scope.findIndex((x) => x.name === p.name))
      ui.openLightbox(scope, idx)
    },
    onDelete: (p) => deleteMut.mutate(p.name),
    onUngroup: (p) => ungroupMut.mutate(p.name),
  }
}
