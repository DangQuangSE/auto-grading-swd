import { useQuery } from "@tanstack/react-query";
import { getClasses } from "../services/classService";

export function useClasses(enabled: boolean) {
  return useQuery({
    queryKey: ["classes"],
    queryFn: getClasses,
    enabled,
  });
}
