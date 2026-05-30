import type { PhotoInfo } from '../api/schemas'

type Props = {
  photo: PhotoInfo
  onOpenLightbox: (p: PhotoInfo) => void
  onStartDrag: (name: string) => void
  onEndDrag: () => void
  onDropOnPhoto: (targetName: string) => void
  onDelete: (p: PhotoInfo) => void
  onUngroup?: (p: PhotoInfo) => void
}

export function PhotoCard({
  photo,
  onOpenLightbox,
  onStartDrag,
  onEndDrag,
  onDropOnPhoto,
  onDelete,
  onUngroup,
}: Props) {
  return (
    <div
      data-photo-name={photo.name}
      draggable
      onDragStart={() => onStartDrag(photo.name)}
      onDragEnd={onEndDrag}
      onDragOver={(e) => e.preventDefault()}
      onDrop={() => onDropOnPhoto(photo.name)}
      className="p-1 border border-rule rounded-sm bg-paper hover:border-accent transition-colors"
    >
      <img
        src={photo.thumbnailUrl ?? photo.url}
        alt={photo.name}
        loading="lazy"
        onClick={(e) => {
          e.stopPropagation()
          onOpenLightbox(photo)
        }}
        className="w-full h-auto rounded-sm cursor-pointer block"
      />
      <div className="flex gap-1 mt-1">
        {onUngroup && (
          <button
            type="button"
            onClick={() => onUngroup(photo)}
            className="px-2 py-0.5 text-xs bg-rule/40 hover:bg-rule rounded-sm"
          >
            Ungroup
          </button>
        )}
        <button
          type="button"
          onClick={() => onDelete(photo)}
          className="px-2 py-0.5 text-xs text-paper bg-danger/90 hover:bg-danger rounded-sm"
        >
          Delete
        </button>
      </div>
    </div>
  )
}
