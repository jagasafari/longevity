import { describe, expect, it } from 'vitest'
import { dateToApi } from '../src/api/client'
import {
  PhotoPageSchema,
  GroupTreeNodeSchema,
} from '../src/api/schemas'

describe('api', () => {
  it('formats date as yyyyMMdd', () => {
    expect(dateToApi(new Date(2024, 0, 5))).toBe('20240105')
    expect(dateToApi(new Date(2024, 11, 31))).toBe('20241231')
  })

  it('parses photo page', () => {
    const data = {
      items: [
        {
          name: 'a.jpg',
          url: '/u/a',
          thumbnailUrl: '/t/a',
          lastModified: '2025-01-01T00:00:00Z',
        },
      ],
      nextBefore: null,
    }
    expect(() => PhotoPageSchema.parse(data)).not.toThrow()
  })

  it('parses group tree node with null parent', () => {
    expect(() =>
      GroupTreeNodeSchema.parse({
        groupId: 'g',
        parentGroupId: null,
        photos: ['a'],
      }),
    ).not.toThrow()
  })
})
