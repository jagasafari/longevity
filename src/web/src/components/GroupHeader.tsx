import type { Category } from '../api/schemas'

type Props = {
  groupId: string
  categories: Category[]
  allCategories: Category[]
  assigning: boolean
  inputValue: string
  onStartAssigning: () => void
  onCancelAssigning: () => void
  onChangeInput: (v: string) => void
  onSave: () => void
  onRemove: (categoryId: number) => void
}

export function GroupHeader({
  groupId,
  categories,
  allCategories,
  assigning,
  inputValue,
  onStartAssigning,
  onCancelAssigning,
  onChangeInput,
  onSave,
  onRemove,
}: Props) {
  const title =
    categories.length > 0 ? categories.map((c) => c.name).join(', ') : 'Group'
  const listId = `cat-suggest-${groupId}`
  return (
    <header className="flex flex-wrap items-center gap-3 mb-3">
      <h2 className="text-lg m-0">{title}</h2>
      <div className="flex flex-wrap gap-1">
        {categories.map((c) => (
          <span
            key={c.id}
            className="inline-flex items-center gap-1 px-2 py-0.5 text-xs bg-accent-soft text-accent rounded-full"
          >
            {c.name}
            <button
              type="button"
              aria-label={`Remove ${c.name}`}
              onClick={() => onRemove(c.id)}
              className="text-accent hover:text-danger"
            >
              ×
            </button>
          </span>
        ))}
      </div>
      <div className="flex gap-1 items-center">
        {assigning ? (
          <>
            <input
              autoFocus
              list={listId}
              value={inputValue}
              onChange={(e) => onChangeInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') onSave()
                if (e.key === 'Escape') onCancelAssigning()
              }}
              placeholder="New or existing category…"
              className="px-2 py-0.5 text-xs border border-rule rounded-sm w-44 bg-paper"
            />
            <datalist id={listId}>
              {allCategories.map((c) => (
                <option key={c.id} value={c.name} />
              ))}
            </datalist>
            <button
              type="button"
              onClick={onSave}
              className="px-2 py-0.5 text-xs text-paper bg-accent rounded-sm"
            >
              Add
            </button>
            <button
              type="button"
              onClick={onCancelAssigning}
              className="px-2 py-0.5 text-xs text-muted hover:text-ink"
            >
              Cancel
            </button>
          </>
        ) : (
          <button
            type="button"
            onClick={onStartAssigning}
            className="px-2 py-0.5 text-xs text-accent border border-accent-soft hover:bg-accent-soft rounded-sm"
          >
            + Category
          </button>
        )}
      </div>
    </header>
  )
}
