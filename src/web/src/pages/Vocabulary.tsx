import { useCallback, useMemo } from 'react'
import {
  usePhotos,
  useGroupTree,
  useGroupPhotos,
  useMoveToGroup,
  useDeletePhoto,
  useUngroup,
  useInvalidateAll,
  useMe,
  useVocabularyGroupIds,
  useCategories,
  useGroupCategories,
} from '../api/hooks'
import { usePhotosHub } from '../api/signalr'
import { useUi } from '../store/ui'
import type { PhotoInfo } from '../api/schemas'
import {
  photosByName,
  childrenByParent,
  lookupPhotos,
  rootGroups,
} from '../lib/groupTree'
import { GroupSection } from '../components/GroupSection'
import type { GroupSectionHandlers } from '../components/GroupSection'
import { Lightbox } from '../components/Lightbox'
import { useTouchDrag } from '../lib/touchDrag'

export function Vocabulary() {
  const me = useMe()
  if (me.isPending) return <p className="text-muted">Loading…</p>
  if (!me.data?.email) return <p className="text-muted">Please sign in.</p>
  return <VocabularyContent />
}

function VocabularyContent() {
  const ui = useUi()
  const inv = useInvalidateAll()

  const photosQuery = usePhotos(null)
  const groupTreeQuery = useGroupTree()
  const vocabQuery = useVocabularyGroupIds()
  const categoriesQuery = useCategories()
  const groupCategoriesQuery = useGroupCategories()

  const allPhotos: PhotoInfo[] = useMemo(
    () => photosQuery.data?.pages.flatMap((p) => p.items) ?? [],
    [photosQuery.data],
  )
  const byName = useMemo(() => photosByName(allPhotos), [allPhotos])
  const tree = groupTreeQuery.data ?? []
  const children = useMemo(() => childrenByParent(tree), [tree])

  const vocabGroupIds = useMemo(
    () => new Set(vocabQuery.data ?? []),
    [vocabQuery.data],
  )

  const categories = categoriesQuery.data ?? []
  const groupCats = groupCategoriesQuery.data ?? {}
  const categoryById = useMemo(
    () => new Map(categories.map((c) => [c.id, c])),
    [categories],
  )
  const groupCategoryList = (gid: string) =>
    (groupCats[gid] ?? [])
      .map((id) => categoryById.get(id))
      .filter((c): c is NonNullable<typeof c> => c !== undefined)

  const isVisible = useCallback(
    (gid: string) => vocabGroupIds.has(gid),
    [vocabGroupIds],
  )

  const visibleRoots = useMemo(
    () => rootGroups(tree, byName, children, isVisible),
    [tree, byName, children, isVisible],
  )

  usePhotosHub(useCallback(() => inv(), [inv]))

  const groupMut = useGroupPhotos()
  const moveMut = useMoveToGroup()
  const deleteMut = useDeletePhoto()
  const ungroupMut = useUngroup()

  const handlers: GroupSectionHandlers = {
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
    onOpenLightbox: (p) => {
      if (ui.draggedPhotoName) return
      ui.openLightbox(p)
    },
    onDelete: (p) => deleteMut.mutate(p.name),
    onUngroup: (p) => ungroupMut.mutate(p.name),
  }

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

  if (photosQuery.isPending) return <p className="text-muted">Loading…</p>

  if (visibleRoots.length === 0) {
    return (
      <p className="text-muted">
        No vocabulary groups yet. Use the <strong>+ Vocabulary</strong> button on
        a group in Gallery to add it here.
      </p>
    )
  }

  return (
    <>
      {visibleRoots.map((node) => (
        <GroupSection
          key={node.groupId}
          node={node}
          depth={0}
          photos={lookupPhotos(node.photos, byName)}
          children={children.get(node.groupId) ?? []}
          childrenByParent={children}
          photosByName={byName}
          isVisible={isVisible}
          handlers={handlers}
          header={
            <header className="flex items-center gap-3 mb-3">
              <h2 className="text-lg m-0">
                {groupCategoryList(node.groupId).map((c) => c.name).join(', ') || 'Group'}
              </h2>
            </header>
          }
        />
      ))}
      <Lightbox photo={ui.lightboxPhoto} onClose={() => ui.openLightbox(null)} />
    </>
  )
}

