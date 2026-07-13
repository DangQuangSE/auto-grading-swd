import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { listUsers, updateUser } from "../services/rosterService";

export function useRosterUsers() {
  return useQuery({
    queryKey: ["roster-users"],
    queryFn: listUsers,
  });
}

export function useUpdateRosterUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      userId,
      studentCode,
      classId,
    }: {
      userId: string;
      studentCode?: string | null;
      classId?: string | null;
    }) => updateUser(userId, { studentCode, classId }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["roster-users"] });
    },
  });
}
