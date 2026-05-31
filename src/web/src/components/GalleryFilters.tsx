import { useCategories, usePhotoCounts } from '../api/hooks'
import { useUi } from '../store/ui'
import { CalendarPopup, dayLabel } from './CalendarPopup'
import { useMemo } from 'react'

export function GalleryFilters() {
  const ui = useUi()
  const categoriesQuery = useCategories()
  const photoCountsQuery = usePhotoCounts()

  const categories = useMemo(() => categoriesQuery.data ?? [], [categoriesQuery.data])

  const counts = useMemo(
    () => new Map((photoCountsQuery.data ?? []).map((c) => [c.date, c.count])),
    [photoCountsQuery.data],
  )

  return (
    <div className="relative flex items-center gap-2">
      <button
        onClick={() => ui.clearFilters()}
        className={[
          'px-3 py-1 text-sm rounded-sm border transition-colors',
          ui.selectedDay === null && ui.selectedCategoryId === null
            ? 'border-accent text-accent font-medium'
            : 'border-rule text-muted hover:text-ink',
        ].join(' ')}
      >
        All
      </button>

      <button
        onClick={() => ui.toggleCalendar()}
        className={[
          'px-3 py-1 text-sm rounded-sm border transition-colors',
          ui.selectedDay
            ? 'border-accent text-accent font-medium'
            : 'border-rule text-muted hover:text-ink',
        ].join(' ')}
      >
        {ui.selectedDay ? dayLabel(ui.selectedDay) : 'Calendar'}
      </button>

      <select
        value={ui.selectedCategoryId ?? ''}
        onChange={(e) =>
          ui.selectCategoryId(e.target.value === '' ? null : Number(e.target.value))
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

      {ui.calendarOpen && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => ui.toggleCalendar()} />
          <CalendarPopup
            month={ui.calendarMonth}
            selected={ui.selectedDay}
            counts={counts}
            onPrev={() =>
              ui.setCalendarMonth(
                new Date(ui.calendarMonth.getFullYear(), ui.calendarMonth.getMonth() - 1, 1),
              )
            }
            onNext={() =>
              ui.setCalendarMonth(
                new Date(ui.calendarMonth.getFullYear(), ui.calendarMonth.getMonth() + 1, 1),
              )
            }
            onPick={(d) => ui.selectDay(d)}
          />
        </>
      )}
    </div>
  )
}
