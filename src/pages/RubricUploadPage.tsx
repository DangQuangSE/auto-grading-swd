import { useState } from "react";
import { ClipboardCheck } from "lucide-react";
import { FileDropzone } from "../components/FileDropzone";

export function RubricUploadPage() {
  const [file, setFile] = useState<File | null>(null);

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Rubric</p>
        <h1>Upload subject criteria</h1>
      </header>
      <form className="form-panel">
        <label>
          Subject
          <select defaultValue="SWD">
            <option value="SWD">SWD</option>
            <option value="SWR">SWR</option>
          </select>
        </label>
        <label>
          Assignment
          <input placeholder="Final project" />
        </label>
        <FileDropzone label="Rubric Word file" accept=".docx" file={file} onChange={setFile} />
        <button className="primary-button" type="button" disabled={!file}>
          <ClipboardCheck aria-hidden="true" />
          Parse rubric
        </button>
      </form>
    </section>
  );
}
