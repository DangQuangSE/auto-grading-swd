import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getSubmissionReviewData, listRecentSubmissions, triggerAiGrading, triggerExtraction } from "../services/gradingService";
import { publishSubmissionGrade, saveFinalCriterionScore } from "../services/reviewService";
import { createSubmission } from "../services/submissionService";

export function useRecentSubmissions() {
  return useQuery({
    queryKey: ["submissions", "recent"],
    queryFn: listRecentSubmissions,
  });
}

export function useSubmissionReview(submissionId?: string) {
  return useQuery({
    queryKey: ["submission-review", submissionId],
    queryFn: () => getSubmissionReviewData(submissionId!),
    enabled: Boolean(submissionId),
  });
}

export function useCreateSubmission() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createSubmission,
    onSuccess: async () => {
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
      await queryClient.invalidateQueries({ queryKey: ["submissions"] });
    },
  });
}

export function useSaveFinalScore() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: saveFinalCriterionScore,
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["submission-review", variables.submissionId] });
      await queryClient.invalidateQueries({ queryKey: ["submissions"] });
    },
  });
}

export function usePublishGrade() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: publishSubmissionGrade,
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["submission-review", variables.submissionId] });
      await queryClient.invalidateQueries({ queryKey: ["submissions"] });
    },
  });
}
