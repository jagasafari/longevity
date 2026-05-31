import { create } from 'zustand'
import type { PhotoInfo } from '../api/schemas'

type LightboxState = { photos: PhotoInfo[]; index: number }

type UiState = {
  view: 'gallery' | 'vocabulary'
  selectedDay: Date | null
  selectedCategoryId: number | null
  draggedPhotoName: string | null
  lightbox: LightboxState | null
  calendarOpen: boolean
  calendarMonth: Date
  assigningGroupId: string | null
  categoryInput: string

  selectDay: (d: Date | null) => void
  selectCategoryId: (id: number | null) => void
  startDrag: (name: string | null) => void
  openLightbox: (photos: PhotoInfo[], index: number) => void
  closeLightbox: () => void
  nextLightbox: () => void
  prevLightbox: () => void
  toggleCalendar: () => void
  setCalendarMonth: (d: Date) => void
  startAssigning: (groupId: string | null) => void
  setCategoryInput: (v: string) => void
  clearFilters: () => void
  setView: (v: 'gallery' | 'vocabulary') => void
}

const firstOfMonth = (d: Date): Date => new Date(d.getFullYear(), d.getMonth(), 1)

const shift = (lb: LightboxState | null, delta: number): LightboxState | null => {
  if (!lb || lb.photos.length === 0) return lb
  const n = lb.photos.length
  return { photos: lb.photos, index: (lb.index + delta + n) % n }
}

export const useUi = create<UiState>((set) => ({
  view: 'gallery',
  selectedDay: null,
  selectedCategoryId: null,
  draggedPhotoName: null,
  lightbox: null,
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
  openLightbox: (photos, index) =>
    set({ lightbox: photos.length > 0 ? { photos, index } : null }),
  closeLightbox: () => set({ lightbox: null }),
  nextLightbox: () => set((s) => ({ lightbox: shift(s.lightbox, 1) })),
  prevLightbox: () => set((s) => ({ lightbox: shift(s.lightbox, -1) })),
  toggleCalendar: () => set((s) => ({ calendarOpen: !s.calendarOpen })),
  setCalendarMonth: (d) => set({ calendarMonth: firstOfMonth(d) }),
  startAssigning: (groupId) => set({ assigningGroupId: groupId, categoryInput: '' }),
  setCategoryInput: (v) => set({ categoryInput: v }),
  clearFilters: () => set({ selectedDay: null, selectedCategoryId: null }),
  setView: (v) => set({ view: v, selectedDay: null, selectedCategoryId: null }),
}))
