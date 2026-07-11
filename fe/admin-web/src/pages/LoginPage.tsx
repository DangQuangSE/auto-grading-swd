import { GoogleLogin, type CredentialResponse } from "@react-oauth/google";
import { useState, type FormEvent } from "react";
import { Navigate } from "react-router-dom";
import { Button } from "../components/ui/Button";
import { Field, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { apiPost, type AdminSession } from "../lib/apiClient";
import { useAuth } from "../providers/AuthProvider";

type LoginResponse = {
  token: string;
  user: {
    id: string;
    email: string;
    role: string;
  };
};

export function LoginPage() {
  const { session, setSession } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (session) {
    return <Navigate to="/" replace />;
  }

  function acceptAdminLogin(response: LoginResponse) {
    if (response.user.role !== "admin" && response.user.role !== "lecturer") {
      throw new Error("This account does not have lecturer or admin access.");
    }

    const session: AdminSession = {
      token: response.token,
      user: { id: response.user.id, email: response.user.email, role: response.user.role },
    };
    setSession(session);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const response = await apiPost<LoginResponse>("/identity/auth/login", { email, password });
      acceptAdminLogin(response);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Sign in failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleGoogleSignIn(credential: CredentialResponse) {
    setError(null);

    if (!credential.credential) {
      setError("Google sign in failed.");
      return;
    }

    setIsSubmitting(true);
    try {
      const response = await apiPost<LoginResponse>("/identity/auth/google", { idToken: credential.credential });
      acceptAdminLogin(response);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Google sign in failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <form className="form-panel" onSubmit={handleSubmit}>
        <h1>Admin sign in</h1>
        <GoogleLogin
          onSuccess={handleGoogleSignIn}
          onError={() => setError("Google sign in failed.")}
          text="continue_with"
        />
        <Field label="Email">
          <TextInput
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
          />
        </Field>
        <Field label="Password">
          <TextInput
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            minLength={6}
            required
          />
        </Field>
        {error ? <FormMessage tone="error">{error}</FormMessage> : null}
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Signing in..." : "Sign in"}
        </Button>
      </form>
    </main>
  );
}
