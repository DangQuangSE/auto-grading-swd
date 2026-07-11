import { GoogleLogin, type CredentialResponse } from "@react-oauth/google";
import { LockKeyhole } from "lucide-react";
import { useState } from "react";
import { Navigate } from "react-router-dom";
import { Button } from "../components/ui/Button";
import { Field, SelectInput, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import type { AppRole } from "../lib/database.types";
import { useAuth } from "../providers/AuthProvider";
import { signInWithEmail, signInWithGoogle, signUpWithEmail } from "../services/authService";

export function LoginPage() {
  const { authNotice, refreshSession, session } = useAuth();
  const [mode, setMode] = useState<"login" | "signup">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [role, setRole] = useState<AppRole>("student");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (session) {
    return <Navigate to="/dashboard" replace />;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setIsSubmitting(true);

    try {
      if (mode === "login") {
        await signInWithEmail(email, password);
        await refreshSession();
      } else {
        await signUpWithEmail({ email, password, fullName, role });
        setMessage("Account created. If email confirmation is enabled, verify your email before signing in.");
        setMode("login");
      }
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Authentication failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleGoogleSignIn(credential: CredentialResponse) {
    setError(null);
    setMessage(null);

    if (!credential.credential) {
      setError("Google sign in failed.");
      return;
    }

    setIsSubmitting(true);
    try {
      await signInWithGoogle(credential.credential);
      await refreshSession();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Google sign in failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Access</p>
        <h1>{mode === "login" ? "Sign in" : "Create account"}</h1>
        <span className="page-note">Only .edu email addresses can access this grading workspace.</span>
      </header>
      <form className="form-panel" onSubmit={handleSubmit}>
        <div className="google-signin-button">
          <GoogleLogin
            onSuccess={handleGoogleSignIn}
            onError={() => setError("Google sign in failed.")}
            text="continue_with"
          />
        </div>
        <div className="form-divider">
          <span>Email</span>
        </div>
        {mode === "signup" ? (
          <>
            <Field label="Full name">
              <TextInput value={fullName} onChange={(event) => setFullName(event.target.value)} required />
            </Field>
            <Field label="Role">
              <SelectInput value={role} onChange={(event) => setRole(event.target.value as AppRole)}>
                <option value="student">Student</option>
                <option value="lecturer">Lecturer</option>
              </SelectInput>
            </Field>
          </>
        ) : null}
        <Field label="Email">
          <TextInput
            type="email"
            placeholder="lecturer@school.edu"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
          />
        </Field>
        <Field label="Password">
          <TextInput
            type="password"
            placeholder="********"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            minLength={6}
            required
          />
        </Field>
        {authNotice ? <FormMessage tone="error">{authNotice}</FormMessage> : null}
        {error ? <FormMessage tone="error">{error}</FormMessage> : null}
        {message ? <FormMessage tone="success">{message}</FormMessage> : null}
        <Button type="submit" disabled={isSubmitting}>
          <LockKeyhole aria-hidden="true" />
          {isSubmitting ? "Working..." : mode === "login" ? "Sign in" : "Create account"}
        </Button>
        <Button
          variant="text"
          type="button"
          onClick={() => {
            setMode(mode === "login" ? "signup" : "login");
            setError(null);
            setMessage(null);
          }}
        >
          {mode === "login" ? "Create a new account" : "Use an existing account"}
        </Button>
      </form>
    </section>
  );
}
