import type { ReactNode } from 'react'
import type { GroupTreeNode, PhotoInfo } from '../api/schemas'
import { PhotoCard } from './PhotoCard'

export type GroupSectionHandlers = {
  onStartDrag: (name: string) => void
  onEndDrag: () => void
  onDropOnPhoto: (targetName: string) => void
  onDropIntoGroup: (groupId: string) => void
  onOpenLightbox: (p: PhotoInfo) => void
  onDelete: (p: PhotoInfo) => void
  onUngroup: (p: PhotoInfo) => void
}

type Props = {
  node: GroupTreeNode
  depth: number
  photos: PhotoInfo[]
  children: GroupTreeNode[]
  childrenByParent: Map<string, GroupTreeNode[]>
  photosByName: Map<string, PhotoInfo>
  isVisible: (groupId: string) => boolean
  handlers: GroupSectionHandlers
  header?: ReactNode
}

export function GroupSection({
  node,
  depth,
  photos,
  children,
  childrenByParent,
  photosByName,
  isVisible,
  handlers,
  header,
}: Props) {
  if (depth > 0 && !isVisible(node.groupId)) return null

  const Heading = depth === 0 ? 'h2' : 'h3'

  return (
    <section
      data-group-id={node.groupId}
      onDragOver={(e) => e.preventDefault()}
      onDrop={() => handlers.onDropIntoGroup(node.groupId)}
      className={
        depth === 0
          ? 'mb-12'
          : 'mt-4 pl-4 border-l border-rule'
      }
    >
      {header ?? (
        <header className="mb-3">
          <Heading className={depth === 0 ? 'text-lg' : 'text-base'}>
            {depth === 0 ? 'Group' : 'Subgroup'}
          </Heading>
        </header>
      )}

      {photos.length > 0 && (
        <div className="grid gap-3 grid-cols-[repeat(auto-fill,minmax(200px,1fr))]">
          {photos.map((p) => (
            <PhotoCard
              key={p.name}
              photo={p}
              onOpenLightbox={handlers.onOpenLightbox}
              onStartDrag={handlers.onStartDrag}
              onEndDrag={handlers.onEndDrag}
              onDropOnPhoto={handlers.onDropOnPhoto}
              onDelete={handlers.onDelete}
              onUngroup={handlers.onUngroup}
            />
          ))}
        </div>
      )}

      {children.length > 0 && (
        <div className="flex flex-col gap-4 mt-4">
          {children.map((c) => {
            const childPhotos = c.photos
              .map((n) => photosByName.get(n))
              .filter((p): p is PhotoInfo => p !== undefined)
              .sort((a, b) => b.lastModified.localeCompare(a.lastModified))
            return (
              <GroupSection
                key={c.groupId}
                node={c}
                depth={depth + 1}
                photos={childPhotos}
                children={childrenByParent.get(c.groupId) ?? []}
                childrenByParent={childrenByParent}
                photosByName={photosByName}
                isVisible={isVisible}
                handlers={handlers}
              />
            )
          })}
        </div>
      )}
    </section>
  )
}
