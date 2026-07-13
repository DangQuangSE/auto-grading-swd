import { apiGet } from "../lib/apiClient";

export type ClassOption = {
  id: string;
  name: string;
};

export async function getClasses() {
  return apiGet<ClassOption[]>("/catalog/classes");
}
