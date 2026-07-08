import { useState } from "react";
import { AppShell, type AppView } from "./components/AppShell";
import { LecturerDashboard } from "./pages/LecturerDashboard";
import { RubricUploadPage } from "./pages/RubricUploadPage";
import { StudentResultPage } from "./pages/StudentResultPage";
import { StudentSubmissionPage } from "./pages/StudentSubmissionPage";
import { SubmissionReviewPage } from "./pages/SubmissionReviewPage";

function App() {
  const [activeView, setActiveView] = useState<AppView>("lecturer");

  const page = {
    lecturer: <LecturerDashboard />,
    rubric: <RubricUploadPage />,
    student: <StudentSubmissionPage />,
    review: <SubmissionReviewPage />,
    result: <StudentResultPage />,
  }[activeView];

  return (
    <AppShell activeView={activeView} onViewChange={setActiveView}>
      {page}
    </AppShell>
  );
}

export default App;
