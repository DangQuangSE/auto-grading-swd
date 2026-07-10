import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import { clearStoredSession, getStoredSession, setStoredSession, type AdminSession } from "../lib/apiClient";

type AuthContextValue = {
  session: AdminSession | null;
  setSession: (session: AdminSession) => void;
  signOut: () => void;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSessionState] = useState<AdminSession | null>(() => getStoredSession());

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      setSession: (next) => {
        setStoredSession(next);
        setSessionState(next);
      },
      signOut: () => {
        clearStoredSession();
        setSessionState(null);
      },
    }),
    [session],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
