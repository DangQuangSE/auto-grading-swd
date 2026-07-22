import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createClass, fetchLecturers, getClasses, updateClass } from "../services/classService";

export function useClasses() {
  return useQuery({ queryKey: ["classes"], queryFn: getClasses });
}

export function useLecturers() {
  return useQuery({ queryKey: ["lecturers"], queryFn: fetchLecturers, staleTime: 30_000 });
}

export function useCreateClass() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createClass,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["classes"] });
    },
  });
}

export function useUpdateClass() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      classId,
      changes,
    }: {
      classId: string;
      changes: { lecturerId?: string; subjectId?: string };
    }) => updateClass(classId, changes),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["classes"] });
    },
  });
}
