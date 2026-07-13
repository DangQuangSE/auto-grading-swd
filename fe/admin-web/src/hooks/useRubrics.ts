import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  confirmRubric,
  listRubrics,
  unlockRubric,
  updateRubricCriteria,
  uploadRubricDocx,
  type RubricCriterionInput,
} from "../services/rubricService";

const RUBRICS_QUERY_KEY = ["rubrics"];

export function useRubrics() {
  return useQuery({
    queryKey: RUBRICS_QUERY_KEY,
    queryFn: () => listRubrics(),
    refetchInterval: (query) => ((query.state.data ?? []).some((rubric) => rubric.status === "parsing") ? 2000 : false),
  });
}

export function useUploadRubric() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: uploadRubricDocx,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: RUBRICS_QUERY_KEY });
    },
  });
}

export function useUpdateRubricCriteria() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ rubricId, criteria }: { rubricId: string; criteria: RubricCriterionInput[] }) =>
      updateRubricCriteria(rubricId, criteria),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: RUBRICS_QUERY_KEY });
    },
  });
}

export function useConfirmRubric() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: confirmRubric,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: RUBRICS_QUERY_KEY });
    },
  });
}

export function useUnlockRubric() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: unlockRubric,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: RUBRICS_QUERY_KEY });
    },
  });
}
