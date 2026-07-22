import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createSubmission } from "../services/submissionService";

export function useCreateSubmission() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createSubmission,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["my-submissions"] });
      await queryClient.invalidateQueries({ queryKey: ["submissions"] });
    },
  });
}
