import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import type { AppSession } from "../lib/apiClient";
import { getCurrentSession, isAllowedEducationEmail, signOut } from "../services/authService";

type AuthContextValue = {
  session: AppSession | null;
  isLoadingSession: boolean;
  authNotice: string | null;
  refreshSession: () => Promise<void>;
  signOutUser: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AppSession | null>(null);
  const [isLoadingSession, setIsLoadingSession] = useState(true);
  const [authNotice, setAuthNotice] = useState<string | null>(null);

  async function acceptSession(nextSession: AppSession | null) {
    if (!nextSession) {
      setSession(null);
      return;
    }

    if (!isAllowedEducationEmail(nextSession.user.email)) {
      await signOut();
      setSession(null);
      setAuthNotice("Only .edu email addresses can access this system. Please use your school email.");
      return;
    }

    setAuthNotice(null);
    setSession(nextSession);
  }

  async function refreshSession() {
    await acceptSession(getCurrentSession());
  }

  async function signOutUser() {
    await signOut();
    setSession(null);
  }

  useEffect(() => {
    refreshSession().finally(() => setIsLoadingSession(false));
    // No server-side session listener like Supabase's onAuthStateChange: the token is
    // self-contained (issued once at login) and only changes via refreshSession/signOutUser.
  }, []);

  const value = useMemo(
    () => ({ session, isLoadingSession, authNotice, refreshSession, signOutUser }),
    [session, isLoadingSession, authNotice],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);

  if (!value) {
    throw new Error("useAuth must be used within AuthProvider.");
  }

  return value;
}
