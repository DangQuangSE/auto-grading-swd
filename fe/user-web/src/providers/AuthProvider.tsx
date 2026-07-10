import type { Session } from "@supabase/supabase-js";
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { supabase } from "../lib/supabaseClient";
import { getCurrentSession, isAllowedEducationEmail, signOut, upsertProfile } from "../services/authService";

type AuthContextValue = {
  session: Session | null;
  isLoadingSession: boolean;
  authNotice: string | null;
  refreshSession: () => Promise<void>;
  signOutUser: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [isLoadingSession, setIsLoadingSession] = useState(true);
  const [authNotice, setAuthNotice] = useState<string | null>(null);

  async function acceptSession(nextSession: Session | null) {
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

  async function refreshSession() {
    await acceptSession(await getCurrentSession());
  }

  async function signOutUser() {
    await signOut();
    setSession(null);
  }

  useEffect(() => {
    refreshSession().finally(() => setIsLoadingSession(false));

    const {
      data: { subscription },
    } = supabase.auth.onAuthStateChange((_event, nextSession) => {
      void acceptSession(nextSession);
    });

    return () => subscription.unsubscribe();
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
