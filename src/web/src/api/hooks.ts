import { useCallback } from 'react'
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'
import { photoApi } from './client'
import type { PhotoPage, SubgroupProposal } from './schemas'

export const qk = {
  me: ['me'] as const,
  photos: (date: Date | null) => ['photos', date?.toISOString() ?? null] as const,
  groupTree: ['group-tree'] as const,
  categories: ['categories'] as const,
  groupCategories: ['group-categories'] as const,
  photoCounts: ['photo-counts'] as const,
  vocabularyGroups: ['vocabulary-groups'] as const,
  unassignedVocabPhotos: ['unassigned-vocab-photos'] as const,
}

export const useMe = () =>
  useQuery({
    queryKey: qk.me,
    queryFn: () => photoApi.me(),
    retry: false,
    staleTime: 5 * 60 * 1000,
  })

export const usePhotos = (date: Date | null) =>
  useInfiniteQuery<PhotoPage, Error>({
    queryKey: qk.photos(date),
    queryFn: ({ pageParam }) => {
      const opts: { date?: Date; before?: string } = {}
      if (date) opts.date = date
      if (typeof pageParam === 'string') opts.before = pageParam
      return photoApi.photos(opts)
    },
    initialPageParam: undefined,
    getNextPageParam: (last) => last.nextBefore ?? undefined,
  })

export const useGroupTree = () =>
  useQuery({ queryKey: qk.groupTree, queryFn: () => photoApi.groupTree() })

export const useCategories = () =>
  useQuery({ queryKey: qk.categories, queryFn: () => photoApi.categories() })

export const useGroupCategories = () =>
  useQuery({
    queryKey: qk.groupCategories,
    queryFn: () => photoApi.groupCategories(),
  })

export const usePhotoCounts = () =>
  useQuery({
    queryKey: qk.photoCounts,
    queryFn: () => photoApi.photoCounts(),
    staleTime: 5 * 60 * 1000,
  })

export const useVocabularyGroups = () =>
  useQuery({
    queryKey: qk.vocabularyGroups,
    queryFn: () => photoApi.vocabularyGroups(),
  })

export const useMoveGroupToVocabulary = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (galleryGroupId: string) =>
      photoApi.moveGalleryGroupToVocabulary(galleryGroupId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.vocabularyGroups })
      void qc.invalidateQueries({ queryKey: qk.groupTree })
      void qc.invalidateQueries({ queryKey: qk.groupCategories })
      void qc.invalidateQueries({ queryKey: qk.categories })
    },
  })
}

export const useRemoveFromVocabulary = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vocabGroupId: string) => photoApi.removeFromVocabulary(vocabGroupId),
    onSuccess: () => void qc.invalidateQueries({ queryKey: qk.vocabularyGroups }),
  })
}

export const useUnassignedVocabPhotos = () =>
  useQuery({
    queryKey: qk.unassignedVocabPhotos,
    queryFn: () => photoApi.unassignedVocabPhotos(),
  })

export const useRenameVocabGroup = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (args: { vocabGroupId: string; name: string }) =>
      photoApi.renameVocabGroup(args.vocabGroupId, args.name),
    onSuccess: () => void qc.invalidateQueries({ queryKey: qk.vocabularyGroups }),
  })
}

export const useRemovePhotoFromVocabGroup = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (args: { vocabGroupId: string; photoName: string }) =>
      photoApi.removePhotoFromVocabGroup(args.vocabGroupId, args.photoName),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.vocabularyGroups })
      void qc.invalidateQueries({ queryKey: qk.unassignedVocabPhotos })
    },
  })
}

export const useAddPhotoToVocabGroup = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (args: { vocabGroupId: string; photoName: string }) =>
      photoApi.addPhotoToVocabGroup(args.vocabGroupId, args.photoName),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.vocabularyGroups })
      void qc.invalidateQueries({ queryKey: qk.unassignedVocabPhotos })
    },
  })
}

export const useInvalidateAll = () => {
  const qc = useQueryClient()
  return useCallback(() => {
    void qc.invalidateQueries({ queryKey: ['photos'] })
    void qc.invalidateQueries({ queryKey: qk.groupTree })
    void qc.invalidateQueries({ queryKey: qk.groupCategories })
    void qc.invalidateQueries({ queryKey: qk.categories })
    void qc.invalidateQueries({ queryKey: qk.vocabularyGroups })
  }, [qc])
}

export const useDeletePhoto = () => {
  const inv = useInvalidateAll()
  return useMutation({
    mutationFn: (name: string) => photoApi.deletePhoto(name),
    onSuccess: inv,
  })
}

export const useUngroup = () => {
  const inv = useInvalidateAll()
  return useMutation({
    mutationFn: (name: string) => photoApi.ungroup(name),
    onSuccess: inv,
  })
}

export const useGroupPhotos = () => {
  const inv = useInvalidateAll()
  return useMutation({
    mutationFn: (args: { sourceName: string; targetName: string }) =>
      photoApi.groupPhotos(args.sourceName, args.targetName),
    onSuccess: inv,
  })
}

export const useMoveToGroup = () => {
  const inv = useInvalidateAll()
  return useMutation({
    mutationFn: (args: { photoName: string; targetGroupId: string }) =>
      photoApi.movePhotoToGroup(args.photoName, args.targetGroupId),
    onSuccess: inv,
  })
}

export const useAssignCategory = () => {
  const inv = useInvalidateAll()
  return useMutation({
    mutationFn: (args: { groupId: string; categoryName: string }) =>
      photoApi.assignCategory(args.groupId, args.categoryName),
    onSuccess: inv,
  })
}

export const useRemoveCategory = () => {
  const inv = useInvalidateAll()
  return useMutation({
    mutationFn: (args: { groupId: string; categoryId: number }) =>
      photoApi.removeCategory(args.groupId, args.categoryId),
    onSuccess: inv,
  })
}

export const useLabelPhoto = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (photoName: string) => photoApi.labelPhoto(photoName),
    onSuccess: () => void qc.invalidateQueries({ queryKey: qk.vocabularyGroups }),
  })
}

export const useLabelAllInGroup = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (groupId: string) => photoApi.labelAllInGroup(groupId),
    onSuccess: () => void qc.invalidateQueries({ queryKey: qk.vocabularyGroups }),
  })
}

export const useMatchSubgroups = () =>
  useMutation({
    mutationFn: (groupId: string) => photoApi.matchSubgroups(groupId),
  })

export const useApplySubgroups = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (args: { groupId: string; proposals: SubgroupProposal[] }) =>
      photoApi.applySubgroups(args.groupId, args.proposals),
    onSuccess: () => void qc.invalidateQueries({ queryKey: qk.vocabularyGroups }),
  })
}

export const useSetPhotoWord = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (args: { photoName: string; word: string }) =>
      photoApi.setPhotoWord(args.photoName, args.word),
    onSuccess: () => void qc.invalidateQueries({ queryKey: qk.vocabularyGroups }),
  })
}
