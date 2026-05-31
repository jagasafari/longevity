import { useEffect } from 'react'
import { useUi } from '../store/ui'

export function Lightbox() {
  const lightbox = useUi((s) => s.lightbox)
  const close = useUi((s) => s.closeLightbox)
  const next = useUi((s) => s.nextLightbox)
  const prev = useUi((s) => s.prevLightbox)

  const open = lightbox !== null

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close()
      else if (e.key === 'ArrowRight') next()
      else if (e.key === 'ArrowLeft') prev()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, close, next, prev])

  if (!lightbox) return null
  const photo = lightbox.photos[lightbox.index]
  if (!photo) return null
  const showNav = lightbox.photos.length > 1

  return (
    <div
      onClick={close}
      className="fixed inset-0 bg-black/90 flex items-center justify-center z-[1000] cursor-pointer fade-in"
    >
      <button
        type="button"
        aria-label="Close"
        onClick={(e) => {
          e.stopPropagation()
          close()
        }}
        className="absolute top-4 right-6 text-paper text-4xl leading-none bg-transparent border-none cursor-pointer"
      >
        ×
      </button>

      {showNav && (
        <button
          type="button"
          aria-label="Previous"
          onClick={(e) => {
            e.stopPropagation()
            prev()
          }}
          className="absolute left-4 top-1/2 -translate-y-1/2 text-paper text-5xl leading-none bg-black/40 hover:bg-black/60 rounded-full w-12 h-12 flex items-center justify-center border-none cursor-pointer"
        >
          ‹
        </button>
      )}

      <img
        src={photo.url}
        alt={photo.name}
        onClick={(e) => e.stopPropagation()}
        className="max-w-[95vw] max-h-[95vh] object-contain cursor-default"
      />

      {showNav && (
        <>
          <button
            type="button"
            aria-label="Next"
            onClick={(e) => {
              e.stopPropagation()
              next()
            }}
            className="absolute right-4 top-1/2 -translate-y-1/2 text-paper text-5xl leading-none bg-black/40 hover:bg-black/60 rounded-full w-12 h-12 flex items-center justify-center border-none cursor-pointer"
          >
            ›
          </button>
          <div className="absolute bottom-4 left-1/2 -translate-x-1/2 text-paper/80 text-sm bg-black/40 px-3 py-1 rounded-sm">
            {lightbox.index + 1} / {lightbox.photos.length}
          </div>
        </>
      )}
    </div>
  )
}
