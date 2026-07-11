import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { DEFAULT_PAGE, DEFAULT_PAGE_SIZE } from "../lib/pagination";
import { createSubject, listAssignments, listSubjects } from "../services/subjectService";

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

export function useAssignments(subjectId?: string) {
  return useQuery({
    queryKey: ["assignments", subjectId],
    queryFn: () => listAssignments(subjectId!),
    enabled: Boolean(subjectId),
  });
}
