import { useMemo } from 'react'

type Cell = { day: Date; key: string } | { day: null; key: string }

export const monthCells = (month: Date): Cell[] => {
  const first = new Date(month.getFullYear(), month.getMonth(), 1)
  const offset = (first.getDay() + 6) % 7
  const daysInMonth = new Date(
    month.getFullYear(),
    month.getMonth() + 1,
    0,
  ).getDate()
  const cells: Cell[] = []
  for (let i = 0; i < offset; i++) cells.push({ day: null, key: `pad-${i}` })
  for (let d = 1; d <= daysInMonth; d++) {
    const day = new Date(month.getFullYear(), month.getMonth(), d)
    cells.push({ day, key: day.toISOString() })
  }
  return cells
}

export const dateKey = (d: Date): string =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

export const monthLabel = (d: Date): string =>
  d.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })

export const dayLabel = (d: Date): string =>
  d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })

type Props = {
  month: Date
  selected: Date | null
  counts: Map<string, number>
  onPrev: () => void
  onNext: () => void
  onPick: (d: Date) => void
}

export function CalendarPopup({
  month,
  selected,
  counts,
  onPrev,
  onNext,
  onPick,
}: Props) {
  const cells = useMemo(() => monthCells(month), [month])
  const selKey = selected ? dateKey(selected) : null
  return (
    <div className="absolute top-[calc(100%+6px)] left-0 z-50 bg-paper border border-rule rounded-sm p-3 fade-in shadow-[0_8px_24px_rgba(0,0,0,0.06)]">
      <div className="flex items-center justify-between mb-2 font-serif text-sm">
        <button
          type="button"
          onClick={onPrev}
          aria-label="Previous month"
          className="px-2 text-muted hover:text-accent"
        >
          ‹
        </button>
        <span>{monthLabel(month)}</span>
        <button
          type="button"
          onClick={onNext}
          aria-label="Next month"
          className="px-2 text-muted hover:text-accent"
        >
          ›
        </button>
      </div>
      <div className="grid grid-cols-7 gap-[2px] text-[0.65rem] text-muted text-center mb-1">
        {['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'].map((d) => (
          <span key={d}>{d}</span>
        ))}
      </div>
      <div className="grid grid-cols-7 gap-[2px]">
        {cells.map((c) =>
          c.day === null ? (
            <span key={c.key} className="w-8 h-9" />
          ) : (
            <DayCell
              key={c.key}
              day={c.day}
              count={counts.get(dateKey(c.day)) ?? 0}
              selected={selKey === dateKey(c.day)}
              onPick={onPick}
            />
          ),
        )}
      </div>
    </div>
  )
}

function DayCell({
  day,
  count,
  selected,
  onPick,
}: {
  day: Date
  count: number
  selected: boolean
  onPick: (d: Date) => void
}) {
  const base =
    'w-8 h-9 flex flex-col items-center justify-center rounded-sm text-xs cursor-pointer'
  const cls = selected
    ? `${base} bg-accent text-paper font-semibold`
    : count > 0
      ? `${base} bg-accent-soft text-accent font-medium`
      : `${base} text-ink hover:bg-rule/40`
  return (
    <button
      type="button"
      className={cls}
      onClick={(e) => {
        e.stopPropagation()
        onPick(day)
      }}
    >
      <span className="leading-none">{day.getDate()}</span>
      {count > 0 && (
        <span className="text-[0.55rem] leading-none mt-[1px]">{count}</span>
      )}
    </button>
  )
}
