import { describe, expect, it } from 'vitest'
import {
  childrenByParent,
  hasVisiblePhotos,
  isGroupVisible,
  lookupPhotos,
  photosByName,
  rootGroups,
  ungroupedPhotos,
} from '../src/lib/groupTree'
import type { GroupTreeNode, PhotoInfo } from '../src/api/schemas'

const photo = (name: string, lastModified: string): PhotoInfo => ({
  name,
  url: `/p/${name}`,
  thumbnailUrl: `/t/${name}`,
  lastModified,
})

const node = (
  groupId: string,
  parentGroupId: string | null,
  photos: string[],
): GroupTreeNode => ({ groupId, parentGroupId, photos })

describe('groupTree', () => {
  const photos = [
    photo('a', '2025-01-03T10:00:00Z'),
    photo('b', '2025-01-02T10:00:00Z'),
    photo('c', '2025-01-01T10:00:00Z'),
    photo('d', '2025-01-04T10:00:00Z'),
  ]
  const tree: GroupTreeNode[] = [
    node('g1', null, ['a', 'b']),
    node('g2', 'g1', ['c']),
    node('g3', null, ['d']),
  ]

  const by = photosByName(photos)
  const children = childrenByParent(tree)

  it('groups children by parent', () => {
    expect(children.get('g1')?.map((n) => n.groupId)).toEqual(['g2'])
    expect(children.has('g3')).toBe(false)
  })

  it('looks up photos sorted desc by lastModified', () => {
    expect(lookupPhotos(['a', 'b'], by).map((p) => p.name)).toEqual(['a', 'b'])
    expect(lookupPhotos(['b', 'a'], by).map((p) => p.name)).toEqual(['a', 'b'])
  })

  it('hasVisiblePhotos walks down children', () => {
    const empty = node('empty', null, [])
    const t = [empty, node('child', 'empty', ['a'])]
    const cb = childrenByParent(t)
    expect(hasVisiblePhotos(empty, by, cb)).toBe(true)
  })

  it('rootGroups returns root nodes ordered by newest photo', () => {
    const roots = rootGroups(tree, by, children, () => true)
    expect(roots.map((n) => n.groupId)).toEqual(['g3', 'g1'])
  })

  it('isGroupVisible considers descendants', () => {
    const groupCats = { g2: [1] }
    expect(isGroupVisible('g1', 1, groupCats, children)).toBe(true)
    expect(isGroupVisible('g3', 1, groupCats, children)).toBe(false)
    expect(isGroupVisible('g3', null, groupCats, children)).toBe(true)
  })

  it('ungroupedPhotos excludes grouped names and is empty when filtering by category', () => {
    expect(ungroupedPhotos(photos, tree, null).map((p) => p.name)).toEqual([])
    const extra = [...photos, photo('e', '2025-01-05T00:00:00Z')]
    expect(ungroupedPhotos(extra, tree, null).map((p) => p.name)).toEqual(['e'])
    expect(ungroupedPhotos(extra, tree, 1)).toEqual([])
  })
})
