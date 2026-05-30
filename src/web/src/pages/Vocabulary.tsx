import { useCallback, useMemo } from 'react'
import {
  usePhotos,
  useGroupTree,
  useCategories,
  useGroupCategories,
  useGroupPhotos,
  useMoveToGroup,
  useDeletePhoto,
  useUngroup,
  useAssignCategory,
  useRemoveCategory,
  useInvalidateAll,
  useMe,
} from '../api/hooks'
import { usePhotosHub } from '../api/signalr'
import { useUi } from '../store/ui'
import type { PhotoInfo } from '../api/schemas'
import {
  photosByName,
  childrenByParent,
  lookupPhotos,
  groupsWithCategory,
  rootGroups,
} from '../lib/groupTree'
import { GroupSection } from '../components/GroupSection'
import type { GroupSectionHandlers } from '../components/GroupSection'
import { GroupHeader } from '../components/GroupHeader'
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
  const categoriesQuery = useCategories()
  const groupCategoriesQuery = useGroupCategories()

  const allPhotos: PhotoInfo[] = useMemo(
    () => photosQuery.data?.pages.flatMap((p) => p.items) ?? [],
    [photosQuery.data],
  )
  const byName = useMemo(() => photosByName(allPhotos), [allPhotos])
  const tree = groupTreeQuery.data ?? []
  const children = useMemo(() => childrenByParent(tree), [tree])
  const categories = categoriesQuery.data ?? []
  const groupCats = groupCategoriesQuery.data ?? {}

  const vocabularyCategoryId = useMemo(
    () => categories.find((c) => c.name.toLowerCase() === 'vocabulary')?.id ?? null,
    [categories],
  )

  const vocabGroupIds = useMemo(
    () =>
      vocabularyCategoryId !== null
        ? groupsWithCategory(tree, groupCats, vocabularyCategoryId)
        : new Set<string>(),
    [tree, groupCats, vocabularyCategoryId],
  )

  const isVisible = useCallback(
    (gid: string) => vocabGroupIds.has(gid),
    [vocabGroupIds],
  )

  const visibleRoots = useMemo(
    () => rootGroups(tree, byName, children, isVisible),
    [tree, byName, children, isVisible],
  )

  const categoryById = useMemo(
    () => new Map(categories.map((c) => [c.id, c])),
    [categories],
  )

  usePhotosHub(useCallback(() => inv(), [inv]))

  const groupMut = useGroupPhotos()
  const moveMut = useMoveToGroup()
  const deleteMut = useDeletePhoto()
  const ungroupMut = useUngroup()
  const assignMut = useAssignCategory()
  const removeMut = useRemoveCategory()

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

  const groupCategoryList = (gid: string) =>
    (groupCats[gid] ?? [])
      .map((id) => categoryById.get(id))
      .filter((c): c is NonNullable<typeof c> => c !== undefined)

  if (photosQuery.isPending) return <p className="text-muted">Loading…</p>

  if (vocabularyCategoryId === null) {
    return (
      <p className="text-muted">
        No vocabulary category yet. Tag a photo group with the category name{' '}
        <strong>vocabulary</strong> to see it here.
      </p>
    )
  }

  if (visibleRoots.length === 0) {
    return (
      <p className="text-muted">
        No vocabulary groups yet. Tag a photo group with the{' '}
        <strong>vocabulary</strong> category to see it here.
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
            <GroupHeader
              groupId={node.groupId}
              categories={groupCategoryList(node.groupId)}
              allCategories={categories}
              assigning={ui.assigningGroupId === node.groupId}
              inputValue={ui.categoryInput}
              onStartAssigning={() => ui.startAssigning(node.groupId)}
              onCancelAssigning={() => ui.startAssigning(null)}
              onChangeInput={(v) => ui.setCategoryInput(v)}
              onSave={() => {
                const name = ui.categoryInput.trim()
                if (!name) return
                assignMut.mutate(
                  { groupId: node.groupId, categoryName: name },
                  { onSuccess: () => ui.startAssigning(null) },
                )
              }}
              onRemove={(categoryId) =>
                removeMut.mutate({ groupId: node.groupId, categoryId })
              }
            />
          }
        />
      ))}
      <Lightbox photo={ui.lightboxPhoto} onClose={() => ui.openLightbox(null)} />
    </>
  )
}

