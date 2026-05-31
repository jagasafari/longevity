import { useCallback } from 'react'
import {
  useInvalidateAll,
  useMe,
  useVocabularyGroups,
  useMoveGroupToVocabulary,
  useRemoveFromVocabulary,
  useSuggestVocabulary,
} from '../api/hooks'
import { usePhotosHub } from '../api/signalr'
import { useUi } from '../store/ui'
import type { PhotoInfo, VocabSuggestion } from '../api/schemas'
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
  const moveToVocab = useMoveGroupToVocabulary()
  const removeGroup = useRemoveFromVocabulary()
  const suggest = useSuggestVocabulary()

  usePhotosHub(useCallback(() => inv(), [inv]))

  if (vocabQuery.isPending) return <p className="text-muted">Loading…</p>

  const groups = vocabQuery.data ?? []
  const suggestions: VocabSuggestion[] = suggest.data ?? []

  return (
    <>
      <div className="flex items-center gap-3 mb-4">
        <button
          className="btn-secondary text-sm"
          onClick={() => suggest.mutate()}
          disabled={suggest.isPending}
        >
          {suggest.isPending ? 'Analysing…' : '✦ Suggest vocabulary'}
        </button>
        {suggest.isError && (
          <span className="text-sm text-red-500">Suggestion failed</span>
        )}
      </div>

      {suggestions.length > 0 && (
        <div className="mb-6 p-4 rounded-xl border border-accent/30 bg-accent/5">
          <p className="text-sm font-medium mb-3">
            AI suggestions — click to add:
          </p>
          <ul className="flex flex-col gap-2">
            {suggestions.map((s) => (
              <li key={s.groupId} className="flex items-center gap-3 text-sm">
                <button
                  className="btn-primary text-xs px-2 py-1"
                  onClick={() =>
                    moveToVocab.mutate(s.groupId, {
                      onSuccess: () => suggest.reset(),
                    })
                  }
                  disabled={moveToVocab.isPending}
                >
                  + Add
                </button>
                <code className="text-muted text-xs">{s.groupId.slice(0, 8)}</code>
                <span className="text-ink">{s.reason}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {groups.length === 0 && suggestions.length === 0 && (
        <p className="text-muted">
          No vocabulary groups yet. Use the <strong>+ Vocabulary</strong> button on
          a group in Gallery to add it here.
        </p>
      )}

      {groups.map((group) => (
        <section key={group.id} className="mb-12">
          <header className="flex items-center gap-3 mb-3">
            <h2 className="text-lg m-0">{group.name}</h2>
            <button
              type="button"
              onClick={() => removeGroup.mutate(group.id)}
              disabled={removeGroup.isPending}
              className="px-2 py-0.5 text-xs rounded-sm border border-rule text-muted hover:text-danger hover:border-danger"
            >
              − Remove
            </button>
          </header>
          {group.subgroups.map((sub) => (
            <div key={sub.id} className="flex gap-2 mb-3">
              {sub.photos.map((photo) => (
                <div key={photo.name} className="flex-1 min-w-0">
                  <VocabPhoto photo={photo} onOpen={(p) => ui.openLightbox(p)} />
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
                  onOpen={(p) => ui.openLightbox(p)}
                />
              ))}
            </div>
          )}
        </section>
      ))}

      <Lightbox photo={ui.lightboxPhoto} onClose={() => ui.openLightbox(null)} />
    </>
  )
}

function VocabPhoto({
  photo,
  onOpen,
}: {
  photo: PhotoInfo
  onOpen: (p: PhotoInfo) => void
}) {
  return (
    <div className="p-1 border border-rule rounded-sm bg-paper hover:border-accent transition-colors">
      <img
        src={photo.thumbnailUrl ?? photo.url}
        alt={photo.name}
        loading="lazy"
        onClick={() => onOpen(photo)}
        className="w-full h-auto rounded-sm cursor-pointer block"
      />
    </div>
  )
}

