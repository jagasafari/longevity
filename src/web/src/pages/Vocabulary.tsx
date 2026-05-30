import { useCallback, useMemo } from 'react'
import {
  usePhotos,
  useGroupTree,
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
  categoriesForGroup,
  childrenByParent,
  lookupPhotos,
  photosByName,
  rootGroups,
} from '../lib/groupTree'
import { useGalleryHandlers } from '../lib/galleryHandlers'
import { GroupSection } from '../components/GroupSection'
import { Lightbox } from '../components/Lightbox'

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

  const isVisible = useCallback(
    (gid: string) => vocabGroupIds.has(gid),
    [vocabGroupIds],
  )

  const visibleRoots = useMemo(
    () => rootGroups(tree, byName, children, isVisible),
    [tree, byName, children, isVisible],
  )

  usePhotosHub(useCallback(() => inv(), [inv]))

  const handlers = useGalleryHandlers()

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
                {categoriesForGroup(node.groupId, groupCats, categoryById)
                  .map((c) => c.name)
                  .join(', ') || 'Group'}
              </h2>
            </header>
          }
        />
      ))}
      <Lightbox photo={ui.lightboxPhoto} onClose={() => ui.openLightbox(null)} />
    </>
  )
}

