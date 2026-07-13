import { useMutation } from "@tanstack/react-query";
import { uploadRosterFile } from "../services/bulkImportService";

export function useUploadRosterFile() {
  return useMutation({
    mutationFn: uploadRosterFile,
  });
}
