import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { listRubrics, uploadRubricDocx } from "../services/rubricService";

export function useRubrics() {
  return useQuery({
    queryKey: ["rubrics"],
    queryFn: () => listRubrics(),
  });
}

export function useUploadRubric() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: uploadRubricDocx,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["rubrics"] });
    },
  });
}
