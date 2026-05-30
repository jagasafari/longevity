import { z } from 'zod'

export const PhotoInfoSchema = z.object({
  name: z.string(),
  url: z.string(),
  thumbnailUrl: z.string().nullable().optional(),
  lastModified: z.string(),
})
export type PhotoInfo = z.infer<typeof PhotoInfoSchema>

export const PhotoPageSchema = z.object({
  items: z.array(PhotoInfoSchema),
  nextBefore: z.string().nullable().optional(),
})
export type PhotoPage = z.infer<typeof PhotoPageSchema>

export const GroupTreeNodeSchema = z.object({
  groupId: z.string(),
  parentGroupId: z.string().nullable().optional(),
  photos: z.array(z.string()),
})
export type GroupTreeNode = z.infer<typeof GroupTreeNodeSchema>

export const CategorySchema = z.object({
  id: z.number(),
  name: z.string(),
})
export type Category = z.infer<typeof CategorySchema>

export const PhotoCountSchema = z.object({
  date: z.string(),
  count: z.number(),
})
export type PhotoCount = z.infer<typeof PhotoCountSchema>

export const MeSchema = z.object({
  email: z.string().nullable().optional(),
})
export type Me = z.infer<typeof MeSchema>

export const GroupCategoriesSchema = z.record(z.string(), z.array(z.number()))
export type GroupCategories = z.infer<typeof GroupCategoriesSchema>
