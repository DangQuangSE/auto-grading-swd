export function assertValidFileExtension(fileName: string, allowedExtensions: string[]) {
  const normalized = fileName.toLowerCase();
  const matches = allowedExtensions.some((extension) => normalized.endsWith(extension));

  if (!matches) {
    throw new Error(`File must use one of these extensions: ${allowedExtensions.join(", ")}`);
  }
}
