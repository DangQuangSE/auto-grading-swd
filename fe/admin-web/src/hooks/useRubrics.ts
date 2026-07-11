import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { listRubrics, uploadRubricDocx } from "../services/rubricService";

export function useRubrics(subjectId?: string) {
  return useQuery({
    queryKey: ["rubrics", subjectId],
    queryFn: () => listRubrics(subjectId!),
    enabled: Boolean(subjectId),
  });
}

export function useUploadRubric() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: uploadRubricDocx,
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["rubrics", variables.subjectId] });
    },
  });
}
