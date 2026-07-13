import { Upload } from "lucide-react";

export function FileDropzone({
  label,
  accept,
  file,
  onChange,
}: {
  label: string;
  accept: string;
  file?: File | null;
  onChange: (file: File | null) => void;
}) {
  return (
    <label className="file-dropzone">
      <Upload aria-hidden="true" />
      <span>{label}</span>
      <strong>{file?.name ?? accept}</strong>
      <input
        type="file"
        accept={accept}
        onChange={(event) => {
          onChange(event.target.files?.[0] ?? null);
          event.target.value = "";
        }}
      />
    </label>
  );
}
