import type { Category, GroupTreeNode, PhotoInfo } from '../api/schemas'

export const photosByName = (photos: PhotoInfo[]): Map<string, PhotoInfo> =>
  new Map(photos.map((p) => [p.name, p]))

export const childrenByParent = (
  tree: GroupTreeNode[],
): Map<string, GroupTreeNode[]> => {
  const m = new Map<string, GroupTreeNode[]>()
  for (const node of tree) {
    const pid = node.parentGroupId
    if (!pid || pid.trim() === '') continue
    const existing = m.get(pid) ?? []
    existing.push(node)
    m.set(pid, existing)
  }
  return m
}

export const lookupPhotos = (
  names: string[],
  by: Map<string, PhotoInfo>,
): PhotoInfo[] =>
  names
    .map((n) => by.get(n))
    .filter((p): p is PhotoInfo => p !== undefined)
    .sort((a, b) => b.lastModified.localeCompare(a.lastModified))

export const isGroupVisible = (
  groupId: string,
  selectedCategoryId: number | null,
  groupCategories: Record<string, number[]>,
  children: Map<string, GroupTreeNode[]>,
): boolean => {
  if (selectedCategoryId === null) return true
  if ((groupCategories[groupId] ?? []).includes(selectedCategoryId)) return true
  const kids = children.get(groupId) ?? []
  return kids.some((c) =>
    isGroupVisible(c.groupId, selectedCategoryId, groupCategories, children),
  )
}

export const hasVisiblePhotos = (
  node: GroupTreeNode,
  by: Map<string, PhotoInfo>,
  children: Map<string, GroupTreeNode[]>,
): boolean => {
  if (lookupPhotos(node.photos, by).length > 0) return true
  const kids = children.get(node.groupId) ?? []
  return kids.some((c) => hasVisiblePhotos(c, by, children))
}

export const rootGroups = (
  tree: GroupTreeNode[],
  by: Map<string, PhotoInfo>,
  children: Map<string, GroupTreeNode[]>,
  isVisible: (id: string) => boolean,
): GroupTreeNode[] =>
  tree
    .filter(
      (n) =>
        (!n.parentGroupId || n.parentGroupId.trim() === '') &&
        isVisible(n.groupId) &&
        hasVisiblePhotos(n, by, children),
    )
    .map((n) => ({ n, photos: lookupPhotos(n.photos, by) }))
    .sort((a, b) => {
      const at = a.photos[0]?.lastModified ?? ''
      const bt = b.photos[0]?.lastModified ?? ''
      return bt.localeCompare(at)
    })
    .map((x) => x.n)

export const ungroupedPhotos = (
  photos: PhotoInfo[],
  tree: GroupTreeNode[],
  selectedCategoryId: number | null,
): PhotoInfo[] => {
  if (selectedCategoryId !== null) return []
  const grouped = new Set(tree.flatMap((g) => g.photos))
  return photos
    .filter((p) => !grouped.has(p.name))
    .sort((a, b) => b.lastModified.localeCompare(a.lastModified))
}

export const categoriesForGroup = (
  groupId: string,
  groupCategories: Record<string, number[]>,
  categoryById: Map<number, Category>,
): Category[] =>
  (groupCategories[groupId] ?? []).flatMap((id) => {
    const c = categoryById.get(id)
    return c ? [c] : []
  })
