import type { Session } from "@supabase/supabase-js";
import { useEffect, useState } from "react";
import { AppShell, type AppView } from "./components/AppShell";
import { supabase } from "./lib/supabaseClient";
import { LecturerDashboard } from "./pages/LecturerDashboard";
import { LoginPage } from "./pages/LoginPage";
import { RubricUploadPage } from "./pages/RubricUploadPage";
import { StudentResultPage } from "./pages/StudentResultPage";
import { StudentSubmissionPage } from "./pages/StudentSubmissionPage";
import { SubmissionReviewPage } from "./pages/SubmissionReviewPage";
import { getCurrentSession, isAllowedEducationEmail, signOut, upsertProfile } from "./services/authService";

function App() {
  const [activeView, setActiveView] = useState<AppView>("lecturer");
  const [session, setSession] = useState<Session | null>(null);
  const [isLoadingSession, setIsLoadingSession] = useState(true);
  const [authNotice, setAuthNotice] = useState<string | null>(null);

  useEffect(() => {
    getCurrentSession()
      .then((nextSession) => handleIncomingSession(nextSession))
      .finally(() => setIsLoadingSession(false));

    const {
      data: { subscription },
    } = supabase.auth.onAuthStateChange((_event, nextSession) => {
      void handleIncomingSession(nextSession);
    });

    return () => subscription.unsubscribe();
  }, []);

  async function handleIncomingSession(nextSession: Session | null) {
    if (!nextSession) {
      setSession(null);
      return;
    }

    const email = nextSession.user.email;

    if (!isAllowedEducationEmail(email)) {
      await signOut();
      setSession(null);
      setAuthNotice("Only .edu email addresses can access this system. Please use your school email.");
      return;
    }

    await upsertProfile({
      id: nextSession.user.id,
      email: email ?? "",
      fullName:
        (nextSession.user.user_metadata.full_name as string | undefined) ??
        (nextSession.user.user_metadata.name as string | undefined) ??
        email ??
        "",
      role: (nextSession.user.user_metadata.role as "student" | "lecturer" | "admin" | undefined) ?? "student",
    });

    setAuthNotice(null);
    setSession(nextSession);
  }

  const page = {
    lecturer: <LecturerDashboard />,
    rubric: <RubricUploadPage />,
    student: <StudentSubmissionPage />,
    review: <SubmissionReviewPage />,
    result: <StudentResultPage />,
  }[activeView];

  async function handleSignOut() {
    await signOut();
    setSession(null);
  }

  if (isLoadingSession) {
    return (
      <main className="auth-shell">
        <p>Loading...</p>
      </main>
    );
  }

  if (!session) {
    return (
      <main className="auth-shell">
        <LoginPage authNotice={authNotice} onSignedIn={() => getCurrentSession().then(handleIncomingSession)} />
      </main>
    );
  }

  return (
    <AppShell
      activeView={activeView}
      onViewChange={setActiveView}
      userEmail={session.user.email}
      onSignOut={handleSignOut}
    >
      {page}
    </AppShell>
  );
}

export default App;
