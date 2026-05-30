import { z } from 'zod'
import {
  CategorySchema,
  GroupCategoriesSchema,
  GroupTreeNodeSchema,
  MeSchema,
  PhotoCountSchema,
  PhotoPageSchema,
  type Category,
  type GroupCategories,
  type GroupTreeNode,
  type Me,
  type PhotoCount,
  type PhotoPage,
} from './schemas'

class HttpError extends Error {
  constructor(public status: number, message: string) {
    super(message)
  }
}

async function request<T>(
  path: string,
  schema: z.ZodSchema<T>,
  init?: RequestInit,
): Promise<T> {
  const res = await fetch(path, {
    credentials: 'same-origin',
    headers: { Accept: 'application/json', ...(init?.headers ?? {}) },
    ...init,
  })
  if (!res.ok) throw new HttpError(res.status, `${res.status} ${path}`)
  const json: unknown = await res.json()
  return schema.parse(json)
}

async function send(
  path: string,
  init: RequestInit,
): Promise<void> {
  const res = await fetch(path, {
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) },
    ...init,
  })
  if (!res.ok) throw new HttpError(res.status, `${res.status} ${path}`)
}

const qs = (params: Record<string, string | number | undefined>): string => {
  const entries = Object.entries(params).filter(
    (e): e is [string, string | number] => e[1] !== undefined,
  )
  if (entries.length === 0) return ''
  const sp = new URLSearchParams()
  for (const [k, v] of entries) sp.set(k, String(v))
  return `?${sp.toString()}`
}

export const dateToApi = (d: Date): string =>
  `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`

export const photoApi = {
  me: (): Promise<Me> => request('/auth/me', MeSchema),

  photos: (opts: { date?: Date; before?: string; limit?: number } = {}): Promise<PhotoPage> =>
    request(
      `/api/photos${qs({
        limit: opts.limit ?? 50,
        date: opts.date ? dateToApi(opts.date) : undefined,
        before: opts.before,
      })}`,
      PhotoPageSchema,
    ),

  groupTree: (): Promise<GroupTreeNode[]> =>
    request('/api/photo-groups/tree', z.array(GroupTreeNodeSchema)),

  categories: (): Promise<Category[]> =>
    request('/api/categories', z.array(CategorySchema)),

  groupCategories: (): Promise<GroupCategories> =>
    request('/api/group-categories', GroupCategoriesSchema),

  photoCounts: (): Promise<PhotoCount[]> =>
    request('/api/photo-counts', z.array(PhotoCountSchema)),

  deletePhoto: (name: string): Promise<void> =>
    send(`/api/photos/${encodeURIComponent(name)}`, { method: 'DELETE' }),

  ungroup: (name: string): Promise<void> =>
    send(`/api/photo-groups/${encodeURIComponent(name)}`, { method: 'DELETE' }),

  groupPhotos: (sourceName: string, targetName: string): Promise<void> =>
    send('/api/photo-groups/group', {
      method: 'POST',
      body: JSON.stringify({ sourceName, targetName }),
    }),

  movePhotoToGroup: (photoName: string, targetGroupId: string): Promise<void> =>
    send('/api/photo-groups/move-to-group', {
      method: 'POST',
      body: JSON.stringify({ photoName, targetGroupId }),
    }),

  assignCategory: (groupId: string, categoryName: string): Promise<void> =>
    send(`/api/group-categories/${encodeURIComponent(groupId)}`, {
      method: 'POST',
      body: JSON.stringify({ categoryName }),
    }),

  removeCategory: (groupId: string, categoryId: number): Promise<void> =>
    send(
      `/api/group-categories/${encodeURIComponent(groupId)}/${categoryId}`,
      { method: 'DELETE' },
    ),
}

export { HttpError }
