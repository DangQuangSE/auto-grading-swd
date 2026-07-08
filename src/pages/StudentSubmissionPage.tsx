import { useState } from "react";
import { Send } from "lucide-react";
import { FileDropzone } from "../components/FileDropzone";

export function StudentSubmissionPage() {
  const [report, setReport] = useState<File | null>(null);
  const [diagram, setDiagram] = useState<File | null>(null);

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Student</p>
        <h1>Submit project files</h1>
      </header>
      <form className="form-panel">
        <label>
          Assignment
          <select defaultValue="final-project">
            <option value="final-project">Final project</option>
          </select>
        </label>
        <FileDropzone label="Report document" accept=".docx" file={report} onChange={setReport} />
        <FileDropzone label="Architecture diagram" accept=".drawio" file={diagram} onChange={setDiagram} />
        <button className="primary-button" type="button" disabled={!report || !diagram}>
          <Send aria-hidden="true" />
          Submit
        </button>
      </form>
    </section>
  );
}
