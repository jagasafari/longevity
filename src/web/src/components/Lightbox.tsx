import type { PhotoInfo } from '../api/schemas'

type Props = {
  photo: PhotoInfo | null
  onClose: () => void
}

export function Lightbox({ photo, onClose }: Props) {
  if (!photo) return null
  return (
    <div
      onClick={onClose}
      className="fixed inset-0 bg-black/90 flex items-center justify-center z-[1000] cursor-pointer fade-in"
    >
      <button
        type="button"
        aria-label="Close"
        onClick={(e) => {
          e.stopPropagation()
          onClose()
        }}
        className="absolute top-4 right-6 text-paper text-4xl leading-none bg-transparent border-none cursor-pointer"
      >
        ×
      </button>
      <img
        src={photo.url}
        alt={photo.name}
        onClick={(e) => e.stopPropagation()}
        className="max-w-[95vw] max-h-[95vh] object-contain cursor-default"
      />
    </div>
  )
}
