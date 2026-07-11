import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createSubject, listAssignments, listSubjects } from "../services/subjectService";

export function useSubjects() {
  return useQuery({ queryKey: ["subjects"], queryFn: listSubjects });
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
