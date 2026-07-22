import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { correctEnrollment, listEnrollments } from "../services/enrollmentService";

export function useAdminEnrollments(studentId: string) {
  return useQuery({
    queryKey: ["admin-enrollments", studentId],
    queryFn: () => listEnrollments(studentId),
    enabled: studentId.length > 0,
  });
}

export function useCorrectEnrollment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: correctEnrollment,
    onSettled: async (_data, _error, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["admin-enrollments", variables.studentId] });
    },
  });
}
