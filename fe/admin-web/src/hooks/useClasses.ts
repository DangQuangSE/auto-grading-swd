import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createClass, fetchLecturers, getClasses, updateClassLecturer } from "../services/classService";

export function useClasses() {
  return useQuery({
    queryKey: ["classes"],
    queryFn: getClasses,
  });
}

export function useLecturers() {
  return useQuery({
    queryKey: ["lecturers"],
    queryFn: fetchLecturers,
    staleTime: 30_000,
  });
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

export function useUpdateClassLecturer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ classId, lecturerId }: { classId: string; lecturerId: string }) =>
      updateClassLecturer(classId, lecturerId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["classes"] });
    },
  });
}
