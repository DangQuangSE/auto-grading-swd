import { useQuery } from "@tanstack/react-query";
import { listAssignments, listSubjects } from "../services/subjectService";

export function useSubjects() {
  return useQuery({
    queryKey: ["subjects"],
    queryFn: listSubjects,
  });
}

export function useAssignments(subjectId?: string) {
  return useQuery({
    queryKey: ["assignments", subjectId],
    queryFn: () => listAssignments(subjectId!),
    enabled: Boolean(subjectId),
  });
}

export function useAllAssignments() {
  return useQuery({
    queryKey: ["all-assignments"],
    queryFn: () => listAssignments(),
  });
}
