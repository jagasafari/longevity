import { create } from 'zustand'
import type { PhotoInfo } from '../api/schemas'

type UiState = {
  view: 'gallery' | 'vocabulary'
  selectedDay: Date | null
  selectedCategoryId: number | null
  draggedPhotoName: string | null
  lightboxPhoto: PhotoInfo | null
  calendarOpen: boolean
  calendarMonth: Date
  assigningGroupId: string | null
  categoryInput: string

  selectDay: (d: Date | null) => void
  selectCategoryId: (id: number | null) => void
  startDrag: (name: string | null) => void
  openLightbox: (p: PhotoInfo | null) => void
  toggleCalendar: () => void
  setCalendarMonth: (d: Date) => void
  startAssigning: (groupId: string | null) => void
  setCategoryInput: (v: string) => void
  clearFilters: () => void
  setView: (v: 'gallery' | 'vocabulary') => void
}

const firstOfMonth = (d: Date): Date => new Date(d.getFullYear(), d.getMonth(), 1)

export const useUi = create<UiState>((set) => ({
  view: 'gallery',
  selectedDay: null,
  selectedCategoryId: null,
  draggedPhotoName: null,
  lightboxPhoto: null,
  calendarOpen: false,
  calendarMonth: firstOfMonth(new Date()),
  assigningGroupId: null,
  categoryInput: '',

  selectDay: (d) =>
    set((s) => ({
      selectedDay: d,
      calendarOpen: false,
      calendarMonth: d ? firstOfMonth(d) : s.calendarMonth,
    })),
  selectCategoryId: (id) => set({ selectedCategoryId: id }),
  startDrag: (name) => set({ draggedPhotoName: name }),
  openLightbox: (p) => set({ lightboxPhoto: p }),
  toggleCalendar: () => set((s) => ({ calendarOpen: !s.calendarOpen })),
  setCalendarMonth: (d) => set({ calendarMonth: firstOfMonth(d) }),
  startAssigning: (groupId) => set({ assigningGroupId: groupId, categoryInput: '' }),
  setCategoryInput: (v) => set({ categoryInput: v }),
  clearFilters: () => set({ selectedDay: null, selectedCategoryId: null }),
  setView: (v) => set({ view: v, selectedDay: null, selectedCategoryId: null }),
}))
