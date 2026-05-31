import { z } from 'zod'
import type { components } from './types.gen'

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
export type Category = components['schemas']['Category']

export const PhotoCountSchema = z.object({
  date: z.string(),
  count: z.number(),
})
export type PhotoCount = components['schemas']['PhotoCount']

export const MeSchema = z.object({
  email: z.string().nullable().optional(),
})
export type Me = components['schemas']['MeResponse']

export const VocabSuggestionSchema = z.object({
  groupId: z.string(),
  reason: z.string(),
})
export type VocabSuggestion = components['schemas']['Suggestion']

export const GroupCategoriesSchema = z.record(z.string(), z.array(z.number()))
export type GroupCategories = z.infer<typeof GroupCategoriesSchema>

export const VocabSubgroupSchema = z.object({
  id: z.string(),
  photos: z.array(PhotoInfoSchema),
})
export type VocabSubgroup = z.infer<typeof VocabSubgroupSchema>

export const VocabGroupSchema = z.object({
  id: z.string(),
  name: z.string(),
  subgroups: z.array(VocabSubgroupSchema),
  ungroupedPhotos: z.array(PhotoInfoSchema),
})
export type VocabGroup = z.infer<typeof VocabGroupSchema>
