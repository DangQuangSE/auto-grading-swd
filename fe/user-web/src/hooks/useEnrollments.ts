import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getClassesBySubject } from "../services/classService";
import { listMyEnrollments, saveMyEnrollment } from "../services/enrollmentService";
import { listOpenSubjects } from "../services/subjectService";

export function useOpenSubjects() {
  return useQuery({ queryKey: ["subjects", "open-registration"], queryFn: listOpenSubjects });
}

export function useSubjectClasses(subjectId: string) {
  return useQuery({
    queryKey: ["classes", "subject", subjectId],
    queryFn: () => getClassesBySubject(subjectId),
    enabled: subjectId.length > 0,
  });
}

export function useMyEnrollments() {
  return useQuery({ queryKey: ["enrollments", "me"], queryFn: listMyEnrollments });
}

export function useSaveMyEnrollment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: saveMyEnrollment,
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: ["enrollments", "me"] });
    },
  });
}
