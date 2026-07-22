import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { DEFAULT_PAGE, DEFAULT_PAGE_SIZE } from "../lib/pagination";
import {
  createAssignment,
  createSubject,
  listAssignments,
  listAllSubjects,
  listSubjects,
  updateSubjectRegistration,
} from "../services/subjectService";

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

export function useAllSubjects() {
  return useQuery({
    queryKey: ["subjects", "all"],
    queryFn: listAllSubjects,
  });
}

export function useUpdateSubjectRegistration() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ subjectId, status }: { subjectId: string; status: "open" | "closed" }) =>
      updateSubjectRegistration(subjectId, status),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["subjects"] });
    },
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
    queryFn: () => listAssignments({ subjectId, page, pageSize }),
    placeholderData: keepPreviousData,
  });
}

export function useCreateAssignment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createAssignment,
    onSuccess: async (_, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["assignments", variables.subjectId] });
      await queryClient.invalidateQueries({ queryKey: ["all-assignments"] });
    },
  });
}

export function useAllAssignments() {
  return useQuery({
    queryKey: ["all-assignments"],
    queryFn: () => listAssignments({}),
  });
}
