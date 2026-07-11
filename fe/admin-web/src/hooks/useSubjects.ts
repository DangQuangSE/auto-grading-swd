import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { DEFAULT_PAGE, DEFAULT_PAGE_SIZE } from "../lib/pagination";
import { createAssignment, createSubject, listAssignments, listSubjects } from "../services/subjectService";

export function useSubjects(params: { page?: number; pageSize?: number; search?: string } = {}) {
  const page = params.page ?? DEFAULT_PAGE;
  const pageSize = params.pageSize ?? DEFAULT_PAGE_SIZE;
  const search = params.search ?? "";

  return useQuery({
    queryKey: ["subjects", page, pageSize, search],
    queryFn: () => listSubjects({ page, pageSize, search }),
    placeholderData: keepPreviousData,
  });
}

export function useCreateSubject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createSubject,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["subjects"] });
    },
  });
}

export function useAssignments(subjectId?: string, params: { page?: number; pageSize?: number } = {}) {
  const page = params.page ?? DEFAULT_PAGE;
  const pageSize = params.pageSize ?? DEFAULT_PAGE_SIZE;

  return useQuery({
    queryKey: ["assignments", subjectId, page, pageSize],
    queryFn: () => listAssignments({ subjectId: subjectId!, page, pageSize }),
    enabled: Boolean(subjectId),
    placeholderData: keepPreviousData,
  });
}

export function useCreateAssignment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createAssignment,
    onSuccess: async (_, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["assignments", variables.subjectId] });
    },
  });
}
