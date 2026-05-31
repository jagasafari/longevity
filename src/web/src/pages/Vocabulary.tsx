import { useCallback, useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import {
  useInvalidateAll,
  useMe,
  useVocabularyGroups,
  useRemoveFromVocabulary,
  useUnassignedVocabPhotos,
  useRenameVocabGroup,
  useRemovePhotoFromVocabGroup,
  useAddPhotoToVocabGroup,
  useLabelPhoto,
  useLabelAllInGroup,
  useMatchSubgroups,
  useApplySubgroups,
  useSetPhotoWord,
  qk,
} from '../api/hooks'
import { usePhotosHub, useLabelStream } from '../api/signalr'
import { useUi } from '../store/ui'
import type { PhotoInfo, SubgroupProposal, VocabGroup } from '../api/schemas'
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

  const vocabQuery = useVocabularyGroups()
  const unassignedQuery = useUnassignedVocabPhotos()
  const removeGroup = useRemoveFromVocabulary()
  const renameGroup = useRenameVocabGroup()
  const removePhoto = useRemovePhotoFromVocabGroup()
  const addPhoto = useAddPhotoToVocabGroup()
  const labelPhoto = useLabelPhoto()
  const labelAll = useLabelAllInGroup()
  const matchSubgroups = useMatchSubgroups()
  const applySubgroups = useApplySubgroups()
  const setWord = useSetPhotoWord()

  const [proposalsByGroup, setProposalsByGroup] = useState<Record<string, SubgroupProposal[]>>({})
  const [activeMatchGroup, setActiveMatchGroup] = useState<string | null>(null)
  const [liveProgress, setLiveProgress] = useState<{ labeled: number; failed: number } | null>(null)
  const qc = useQueryClient()

  const [editingGroupId, setEditingGroupId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')
  const [hiddenGroupIds, setHiddenGroupIds] = useState<Set<string>>(() => {
    try {
      const raw = localStorage.getItem('vocab-hidden-groups')
      return new Set<string>(raw ? JSON.parse(raw) : [])
    } catch { return new Set<string>() }
  })
  const [filterOpen, setFilterOpen] = useState(false)

  useEffect(() => {
    try {
      localStorage.setItem('vocab-hidden-groups', JSON.stringify([...hiddenGroupIds]))
    } catch { /* ignore */ }
  }, [hiddenGroupIds])

  const toggleGroupHidden = (id: string) => {
    setHiddenGroupIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id); else next.add(id)
      return next
    })
  }

  usePhotosHub(useCallback(() => inv(), [inv]))

  useLabelStream(useCallback((evt) => {
    qc.setQueryData<VocabGroup[]>(qk.vocabularyGroups, (prev) => {
      if (!prev) return prev
      if (evt.kind === 'failed') return prev
      const r = evt.result
      const patch = (p: PhotoInfo): PhotoInfo =>
        p.name === r.photoName
          ? { ...p, word: r.word, source: r.source, confidence: r.confidence }
          : p
      return prev.map((g) => ({
        ...g,
        ungroupedPhotos: g.ungroupedPhotos.map(patch),
        subgroups: g.subgroups.map((s) => ({ ...s, photos: s.photos.map(patch) })),
      }))
    })
    setLiveProgress((prev) => {
      const base = prev ?? { labeled: 0, failed: 0 }
      return evt.kind === 'labeled'
        ? { ...base, labeled: base.labeled + 1 }
        : { ...base, failed: base.failed + 1 }
    })
  }, [qc]))

  if (vocabQuery.isPending) return <p className="text-muted">Loading…</p>

  const groups = vocabQuery.data ?? []
  const visibleGroups = groups.filter((g) => !hiddenGroupIds.has(g.id))
  const unassigned = unassignedQuery.data ?? []

  return (
    <>
      {liveProgress && (
        <div className="fixed top-4 right-4 z-50 px-3 py-2 rounded-sm bg-paper border border-accent text-sm flex items-center gap-3 shadow-lg">
          <span className="text-accent">✦ Labeling: {liveProgress.labeled} done</span>
          {liveProgress.failed > 0 && (
            <span className="text-danger">{liveProgress.failed} failed</span>
          )}
          <button
            type="button"
            className="text-xs text-muted hover:text-fg"
            onClick={() => setLiveProgress(null)}
          >×</button>
        </div>
      )}
      <div className="flex items-center gap-3 mb-4">
        {groups.length > 0 && (
          <div className="relative ml-auto">
            <button
              type="button"
              className="btn-secondary text-sm"
              onClick={() => setFilterOpen((o) => !o)}
            >
              Filter: {hiddenGroupIds.size === 0
                ? `All (${groups.length})`
                : `${visibleGroups.length} of ${groups.length}`}
            </button>
            {filterOpen && (
              <div className="absolute right-0 top-full mt-1 z-10 w-64 max-h-80 overflow-y-auto rounded-md border border-rule bg-paper shadow-lg p-2">
                <div className="flex justify-between items-center mb-2 px-1">
                  <button
                    type="button"
                    className="text-xs text-accent hover:underline"
                    onClick={() => setHiddenGroupIds(new Set())}
                  >
                    Show all
                  </button>
                  <button
                    type="button"
                    className="text-xs text-muted hover:underline"
                    onClick={() => setHiddenGroupIds(new Set(groups.map((g) => g.id)))}
                  >
                    Hide all
                  </button>
                </div>
                {groups.map((g) => (
                  <label
                    key={g.id}
                    className="flex items-center gap-2 px-1 py-1 text-sm cursor-pointer hover:bg-accent/5 rounded-sm"
                  >
                    <input
                      type="checkbox"
                      checked={!hiddenGroupIds.has(g.id)}
                      onChange={() => toggleGroupHidden(g.id)}
                    />
                    <span className="truncate">{g.name}</span>
                  </label>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {groups.length === 0 && (
        <p className="text-muted">
          No vocabulary groups yet. Use the <strong>+ Vocabulary</strong> button on
          a group in Gallery to add it here.
        </p>
      )}

      {unassigned.length > 0 && (
        <div className="mb-8 p-4 rounded-xl border border-amber-400/40 bg-amber-50/5">
          <p className="text-sm font-medium mb-3 text-amber-400">
            {unassigned.length} unassigned photo{unassigned.length !== 1 ? 's' : ''} — removed from groups:
          </p>
          <div className="grid gap-3 grid-cols-[repeat(auto-fill,minmax(180px,1fr))]">
            {unassigned.map((photo) => (
              <VocabPhoto
                key={photo.name}
                photo={photo}
                scope={unassigned}
                groups={groups}
                onOpen={(p, s) => ui.openLightbox(s, s.indexOf(p))}
                onMove={(targetGroupId) =>
                  addPhoto.mutate({ vocabGroupId: targetGroupId, photoName: photo.name })
                }
                onLabel={() => labelPhoto.mutate(photo.name)}
                onSetWord={(word) => setWord.mutate({ photoName: photo.name, word })}
                labelPending={labelPhoto.isPending && labelPhoto.variables === photo.name}
              />
            ))}
          </div>
        </div>
      )}

      {visibleGroups.map((group) => (
        <section key={group.id} className="mb-12">
          <header className="flex items-center gap-3 mb-3">
            {editingGroupId === group.id ? (
              <form
                className="flex items-center gap-2"
                onSubmit={(e) => {
                  e.preventDefault()
                  if (editingName.trim()) {
                    renameGroup.mutate(
                      { vocabGroupId: group.id, name: editingName.trim() },
                      { onSuccess: () => setEditingGroupId(null) },
                    )
                  }
                }}
              >
                <input
                  autoFocus
                  className="text-lg font-medium bg-transparent border-b border-accent outline-none"
                  value={editingName}
                  onChange={(e) => setEditingName(e.target.value)}
                />
                <button type="submit" className="btn-primary text-xs px-2 py-1" disabled={renameGroup.isPending}>
                  Save
                </button>
                <button type="button" className="btn-secondary text-xs px-2 py-1" onClick={() => setEditingGroupId(null)}>
                  Cancel
                </button>
              </form>
            ) : (
              <h2
                className="text-lg m-0 cursor-pointer hover:text-accent"
                title="Click to rename"
                onClick={() => { setEditingGroupId(group.id); setEditingName(group.name) }}
              >
                {group.name}
              </h2>
            )}
            {group.subgroups.length === 0 && group.ungroupedPhotos.length === 0 && (
              <button
                type="button"
                onClick={() => removeGroup.mutate(group.id)}
                disabled={removeGroup.isPending}
                className="px-2 py-0.5 text-xs rounded-sm border border-danger text-danger hover:bg-danger/10"
              >
                Delete empty group
              </button>
            )}
            <GroupAiControls
              group={group}
              labelAllPending={labelAll.isPending && labelAll.variables === group.id}
              matchPending={matchSubgroups.isPending && activeMatchGroup === group.id}
              onLabelAll={() => {
                setLiveProgress({ labeled: 0, failed: 0 })
                labelAll.mutate(group.id)
              }}
              onMatch={() => {
                setActiveMatchGroup(group.id)
                matchSubgroups.mutate(group.id, {
                  onSuccess: (proposals) =>
                    setProposalsByGroup((m) => ({ ...m, [group.id]: proposals })),
                })
              }}
            />
          </header>

          {(() => {
            const proposals = proposalsByGroup[group.id]
            if (!proposals) return null
            return (
              <ProposalsPanel
                proposals={proposals}
                applying={applySubgroups.isPending}
                onApply={(toApply) =>
                  applySubgroups.mutate(
                    { groupId: group.id, proposals: toApply },
                    {
                      onSuccess: () =>
                        setProposalsByGroup((m) => {
                          const next = { ...m }
                          delete next[group.id]
                          return next
                        }),
                    },
                  )
                }
                onDismiss={() =>
                  setProposalsByGroup((m) => {
                    const next = { ...m }
                    delete next[group.id]
                    return next
                  })
                }
              />
            )
          })()}

          {group.subgroups.map((sub) => (
            <div key={sub.id} className="flex gap-2 mb-3">
              {sub.photos.map((photo) => (
                <div key={photo.name} className="flex-1 min-w-0">
                  <VocabPhoto
                    photo={photo}
                    scope={sub.photos}
                    groups={groups}
                    onOpen={(p, s) => ui.openLightbox(s, s.indexOf(p))}
                    onMove={(targetGroupId) =>
                      addPhoto.mutate({ vocabGroupId: targetGroupId, photoName: photo.name })
                    }
                    onRemove={() =>
                      removePhoto.mutate({ vocabGroupId: group.id, photoName: photo.name })
                    }
                    onLabel={() => labelPhoto.mutate(photo.name)}
                    onSetWord={(word) => setWord.mutate({ photoName: photo.name, word })}
                    labelPending={labelPhoto.isPending && labelPhoto.variables === photo.name}
                  />
                </div>
              ))}
            </div>
          ))}
          {group.ungroupedPhotos.length > 0 && (
            <div className="grid gap-3 grid-cols-[repeat(auto-fill,minmax(200px,1fr))]">
              {group.ungroupedPhotos.map((photo) => (
                <VocabPhoto
                  key={photo.name}
                  photo={photo}
                  scope={group.ungroupedPhotos}
                  groups={groups}
                  onOpen={(p, s) => ui.openLightbox(s, s.indexOf(p))}
                  onMove={(targetGroupId) =>
                    addPhoto.mutate({ vocabGroupId: targetGroupId, photoName: photo.name })
                  }
                  onRemove={() =>
                    removePhoto.mutate({ vocabGroupId: group.id, photoName: photo.name })
                  }
                  onLabel={() => labelPhoto.mutate(photo.name)}
                  onSetWord={(word) => setWord.mutate({ photoName: photo.name, word })}
                  labelPending={labelPhoto.isPending && labelPhoto.variables === photo.name}
                />
              ))}
            </div>
          )}
        </section>
      ))}

      <Lightbox />
    </>
  )
}

function VocabPhoto({
  photo,
  scope,
  groups,
  onOpen,
  onMove,
  onRemove,
  onLabel,
  onSetWord,
  labelPending,
}: {
  photo: PhotoInfo
  scope: PhotoInfo[]
  groups: VocabGroup[]
  onOpen: (p: PhotoInfo, scope: PhotoInfo[]) => void
  onMove?: (targetGroupId: string) => void
  onRemove?: () => void
  onLabel?: () => void
  onSetWord?: (word: string) => void
  labelPending?: boolean
}) {
  const [editingWord, setEditingWord] = useState(false)
  const [wordDraft, setWordDraft] = useState(photo.word ?? '')
  useEffect(() => { setWordDraft(photo.word ?? '') }, [photo.word])
  return (
    <div className="relative group p-1 border border-rule rounded-sm bg-paper hover:border-accent transition-colors">
      <img
        src={photo.thumbnailUrl ?? photo.url}
        alt={photo.name}
        loading="lazy"
        onClick={() => onOpen(photo, scope)}
        className="w-full h-auto rounded-sm cursor-pointer block"
      />
      {photo.word && !editingWord && (
        <button
          type="button"
          title={photo.source ? `Source: ${photo.source}` : 'Click to edit'}
          onClick={(e) => { e.stopPropagation(); setEditingWord(true) }}
          className="absolute top-2 left-2 px-2 py-0.5 text-xs rounded-sm bg-paper/90 border border-accent text-accent font-medium"
        >
          {photo.word}
        </button>
      )}
      {editingWord && onSetWord && (
        <form
          className="absolute top-2 left-2 right-2 flex gap-1"
          onClick={(e) => e.stopPropagation()}
          onSubmit={(e) => {
            e.preventDefault()
            onSetWord(wordDraft.trim())
            setEditingWord(false)
          }}
        >
          <input
            autoFocus
            value={wordDraft}
            onChange={(e) => setWordDraft(e.target.value)}
            className="flex-1 min-w-0 text-xs px-1 py-0.5 bg-paper border border-accent rounded-sm"
          />
          <button type="submit" className="text-xs px-1 py-0.5 border border-accent rounded-sm text-accent">OK</button>
          <button type="button" className="text-xs px-1 py-0.5 border border-rule rounded-sm" onClick={() => setEditingWord(false)}>×</button>
        </form>
      )}
      <div className="absolute inset-0 flex flex-col justify-end opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none p-1 gap-1">
        {onLabel && !photo.word && (
          <button
            type="button"
            className="pointer-events-auto text-xs bg-paper border border-rule rounded-sm px-1 py-0.5 w-full hover:border-accent hover:text-accent"
            disabled={labelPending}
            onClick={(e) => { e.stopPropagation(); onLabel() }}
          >
            {labelPending ? 'Labeling…' : '✦ Label this'}
          </button>
        )}
        {onMove && groups.length > 0 && (
          <select
            className="pointer-events-auto text-xs bg-paper border border-rule rounded-sm px-1 py-0.5 w-full cursor-pointer"
            defaultValue=""
            onChange={(e) => {
              if (e.target.value) onMove(e.target.value)
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <option value="" disabled>Move to…</option>
            {groups.map((g) => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </select>
        )}
        {onRemove && (
          <button
            type="button"
            className="pointer-events-auto text-xs bg-paper border border-rule rounded-sm px-1 py-0.5 w-full text-muted hover:text-danger hover:border-danger"
            onClick={(e) => { e.stopPropagation(); onRemove() }}
          >
            × Remove
          </button>
        )}
      </div>
    </div>
  )
}

function GroupAiControls({
  group,
  labelAllPending,
  matchPending,
  onLabelAll,
  onMatch,
}: {
  group: VocabGroup
  labelAllPending: boolean
  matchPending: boolean
  onLabelAll: () => void
  onMatch: () => void
}) {
  const allPhotos = [
    ...group.ungroupedPhotos,
    ...group.subgroups.flatMap((s) => s.photos),
  ]
  const unlabeled = allPhotos.filter((p) => !p.word).length
  const labeledUngrouped = group.ungroupedPhotos.filter((p) => p.word).length
  return (
    <>
      {unlabeled > 0 && (
        <button
          type="button"
          onClick={onLabelAll}
          disabled={labelAllPending}
          className="px-2 py-0.5 text-xs rounded-sm border border-accent text-accent hover:bg-accent/10 disabled:opacity-50"
        >
          {labelAllPending ? 'Labeling…' : `✦ Label all unlabeled (${unlabeled})`}
        </button>
      )}
      {labeledUngrouped >= 2 && (
        <button
          type="button"
          onClick={onMatch}
          disabled={matchPending}
          className="px-2 py-0.5 text-xs rounded-sm border border-accent text-accent hover:bg-accent/10 disabled:opacity-50"
        >
          {matchPending ? 'Matching…' : '✦ Match subgroups'}
        </button>
      )}
    </>
  )
}

function ProposalsPanel({
  proposals,
  applying,
  onApply,
  onDismiss,
}: {
  proposals: SubgroupProposal[]
  applying: boolean
  onApply: (proposals: SubgroupProposal[]) => void
  onDismiss: () => void
}) {
  const [accepted, setAccepted] = useState<Set<string>>(
    () => new Set(proposals.map((p) => p.word)),
  )
  const toggle = (word: string) =>
    setAccepted((prev) => {
      const next = new Set(prev)
      if (next.has(word)) next.delete(word); else next.add(word)
      return next
    })
  if (proposals.length === 0) {
    return (
      <div className="mb-4 p-3 rounded-sm border border-rule bg-paper text-sm flex items-center gap-3">
        <span className="text-muted">No matching subgroups found.</span>
        <button type="button" className="btn-secondary text-xs px-2 py-1 ml-auto" onClick={onDismiss}>
          Dismiss
        </button>
      </div>
    )
  }
  const toApply = proposals.filter((p) => accepted.has(p.word))
  return (
    <div className="mb-4 p-3 rounded-sm border border-accent/40 bg-accent/5">
      <p className="text-sm font-medium mb-2">
        {proposals.length} proposed subgroup{proposals.length !== 1 ? 's' : ''}:
      </p>
      <ul className="space-y-1 mb-3">
        {proposals.map((p) => (
          <li key={p.word} className="flex items-center gap-2 text-sm">
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={accepted.has(p.word)} onChange={() => toggle(p.word)} />
              <span className="font-medium text-accent">{p.word}</span>
              <span className="text-muted">({p.photoNames.length} photos)</span>
            </label>
          </li>
        ))}
      </ul>
      <div className="flex gap-2">
        <button
          type="button"
          className="btn-primary text-xs px-3 py-1"
          disabled={applying || toApply.length === 0}
          onClick={() => onApply(toApply)}
        >
          {applying ? 'Applying…' : `Apply ${toApply.length} subgroup${toApply.length !== 1 ? 's' : ''}`}
        </button>
        <button type="button" className="btn-secondary text-xs px-3 py-1" onClick={onDismiss}>
          Dismiss
        </button>
      </div>
    </div>
  )
}

