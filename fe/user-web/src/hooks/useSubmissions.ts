import { useMutation, useQueryClient } from "@tanstack/react-query";
import { triggerAiGrading, triggerExtraction } from "../services/gradingService";
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

export function useRunExtraction() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ submissionId, actorId }: { submissionId: string; actorId?: string }) =>
      triggerExtraction(submissionId, actorId),
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["submission-review", variables.submissionId] });
      await queryClient.invalidateQueries({ queryKey: ["my-submissions"] });
      await queryClient.invalidateQueries({ queryKey: ["submissions"] });
    },
  });
}

export function useRunAiGrading() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ submissionId, actorId }: { submissionId: string; actorId?: string }) =>
      triggerAiGrading(submissionId, actorId),
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["submission-review", variables.submissionId] });
      await queryClient.invalidateQueries({ queryKey: ["my-submissions"] });
      await queryClient.invalidateQueries({ queryKey: ["submissions"] });
    },
  });
}
