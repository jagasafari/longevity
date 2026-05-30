import { useCallback, useMemo, type ReactNode } from 'react'
import {
  useAssignCategory,
  useCategories,
  useGroupCategories,
  useGroupTree,
  useInvalidateAll,
  useMe,
  usePhotoCounts,
  usePhotos,
  useRemoveCategory,
  useVocabularyGroupIds,
  useAddToVocabulary,
  useRemoveFromVocabulary,
} from '../api/hooks'
import { usePhotosHub } from '../api/signalr'
import type { PhotoInfo } from '../api/schemas'
import { useUi } from '../store/ui'
import {
  categoriesForGroup,
  childrenByParent,
  isGroupVisible,
  lookupPhotos,
  photosByName,
  rootGroups,
  ungroupedPhotos,
} from '../lib/groupTree'
import { useGalleryHandlers } from '../lib/galleryHandlers'
import { CalendarPopup, dayLabel } from '../components/CalendarPopup'
import { GroupHeader } from '../components/GroupHeader'
import { GroupSection } from '../components/GroupSection'
import { Lightbox } from '../components/Lightbox'
import { PhotoCard } from '../components/PhotoCard'

export function Home() {
  const me = useMe()
  if (me.isPending) return <p className="text-muted">Loading…</p>
  if (!me.data?.email) return <p className="text-muted">Please sign in.</p>
  return <SignedInHome />
}

function SignedInHome() {
  const ui = useUi()
  const inv = useInvalidateAll()

  const photosQuery = usePhotos(ui.selectedDay)
  const groupTreeQuery = useGroupTree()
  const categoriesQuery = useCategories()
  const groupCategoriesQuery = useGroupCategories()
  const photoCountsQuery = usePhotoCounts()
  const vocabQuery = useVocabularyGroupIds()

  const allPhotos: PhotoInfo[] = useMemo(
    () => photosQuery.data?.pages.flatMap((p) => p.items) ?? [],
    [photosQuery.data],
  )
  const byName = useMemo(() => photosByName(allPhotos), [allPhotos])
  const tree = groupTreeQuery.data ?? []
  const children = useMemo(() => childrenByParent(tree), [tree])
  const categories = categoriesQuery.data ?? []
  const groupCats = groupCategoriesQuery.data ?? {}
  const counts = useMemo(
    () =>
      new Map(
        (photoCountsQuery.data ?? []).map((c) => [
          c.date,
          c.count,
        ]),
      ),
    [photoCountsQuery.data],
  )

  const categoryById = useMemo(
    () => new Map(categories.map((c) => [c.id, c])),
    [categories],
  )

  const vocabGroupIds = useMemo(
    () => new Set(vocabQuery.data ?? []),
    [vocabQuery.data],
  )

  const isVisible = useCallback(
    (gid: string) =>
      !vocabGroupIds.has(gid) &&
      isGroupVisible(gid, ui.selectedCategoryId, groupCats, children),
    [ui.selectedCategoryId, groupCats, children, vocabGroupIds],
  )

  const visibleRoots = useMemo(
    () => rootGroups(tree, byName, children, isVisible),
    [tree, byName, children, isVisible],
  )
  const ungrouped = useMemo(
    () => ungroupedPhotos(allPhotos, tree, ui.selectedCategoryId),
    [allPhotos, tree, ui.selectedCategoryId],
  )

  usePhotosHub(useCallback(() => inv(), [inv]))

  const handlers = useGalleryHandlers()
  const assignMut = useAssignCategory()
  const removeMut = useRemoveCategory()
  const addToVocab = useAddToVocabulary()
  const removeFromVocab = useRemoveFromVocabulary()

  const groupCategoryList = (gid: string) =>
    categoriesForGroup(gid, groupCats, categoryById)

  return (
    <>
      <div className="relative flex flex-wrap items-center gap-2 mb-6">
        <FilterButton
          active={ui.selectedDay === null && ui.selectedCategoryId === null}
          onClick={() => ui.clearFilters()}
        >
          All
        </FilterButton>
        <FilterButton
          active={false}
          onClick={() => ui.toggleCalendar()}
        >
          {ui.selectedDay ? dayLabel(ui.selectedDay) : 'Calendar'}
        </FilterButton>
        <select
          value={ui.selectedCategoryId ?? ''}
          onChange={(e) =>
            ui.selectCategoryId(
              e.target.value === '' ? null : Number(e.target.value),
            )
          }
          className="px-2 py-1 text-sm border border-rule rounded-sm bg-paper"
        >
          <option value="">All categories</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        {photosQuery.hasNextPage && (
          <FilterButton
            active={false}
            onClick={() => void photosQuery.fetchNextPage()}
            disabled={photosQuery.isFetchingNextPage}
          >
            {photosQuery.isFetchingNextPage ? 'Loading…' : 'Load more'}
          </FilterButton>
        )}
        {ui.calendarOpen && (
          <>
            <div
              className="fixed inset-0 z-40"
              onClick={() => ui.toggleCalendar()}
            />
            <CalendarPopup
              month={ui.calendarMonth}
              selected={ui.selectedDay}
              counts={counts}
              onPrev={() =>
                ui.setCalendarMonth(
                  new Date(
                    ui.calendarMonth.getFullYear(),
                    ui.calendarMonth.getMonth() - 1,
                    1,
                  ),
                )
              }
              onNext={() =>
                ui.setCalendarMonth(
                  new Date(
                    ui.calendarMonth.getFullYear(),
                    ui.calendarMonth.getMonth() + 1,
                    1,
                  ),
                )
              }
              onPick={(d) => ui.selectDay(d)}
            />
          </>
        )}
      </div>

      {photosQuery.isPending ? (
        <p className="text-muted">Loading photos…</p>
      ) : allPhotos.length === 0 ? (
        <p className="text-muted">
          {ui.selectedDay ? 'No photos for selected day.' : 'No photos yet.'}
        </p>
      ) : (
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
                  inVocabulary={vocabGroupIds.has(node.groupId)}
                  onToggleVocabulary={() =>
                    vocabGroupIds.has(node.groupId)
                      ? removeFromVocab.mutate(node.groupId)
                      : addToVocab.mutate(node.groupId)
                  }
                />
              }
            />
          ))}

          {ungrouped.length > 0 && (
            <section className="mb-12">
              <h2 className="text-lg mb-3">Ungrouped</h2>
              <div className="grid gap-3 grid-cols-[repeat(auto-fill,minmax(200px,1fr))]">
                {ungrouped.map((p) => (
                  <PhotoCard
                    key={p.name}
                    photo={p}
                    onOpenLightbox={handlers.onOpenLightbox}
                    onStartDrag={handlers.onStartDrag}
                    onEndDrag={handlers.onEndDrag}
                    onDropOnPhoto={handlers.onDropOnPhoto}
                    onDelete={handlers.onDelete}
                  />
                ))}
              </div>
            </section>
          )}

          {photosQuery.hasNextPage && (
            <div className="text-center mt-6">
              <FilterButton
                active={false}
                onClick={() => void photosQuery.fetchNextPage()}
                disabled={photosQuery.isFetchingNextPage}
              >
                {photosQuery.isFetchingNextPage ? 'Loading…' : 'Load more'}
              </FilterButton>
            </div>
          )}
        </>
      )}

      <Lightbox photo={ui.lightboxPhoto} onClose={() => ui.openLightbox(null)} />
    </>
  )
}

function FilterButton({
  children,
  active,
  onClick,
  disabled,
}: {
  children: ReactNode
  active: boolean
  onClick: () => void
  disabled?: boolean
}) {
  const base =
    'px-3 py-1.5 text-sm rounded-sm border transition-colors cursor-pointer'
  const cls = active
    ? `${base} bg-accent text-paper border-accent`
    : `${base} bg-paper text-ink border-rule hover:border-accent`
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={cls}
    >
      {children}
    </button>
  )
}

