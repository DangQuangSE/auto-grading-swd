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

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const response = await apiPost<LoginResponse>("/identity/auth/login", { email, password });
      if (response.user.role !== "admin") {
        throw new Error("This account does not have admin access.");
      }

      const session: AdminSession = {
        token: response.token,
        user: { id: response.user.id, email: response.user.email, role: "admin" },
      };
      setSession(session);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Sign in failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <form className="form-panel" onSubmit={handleSubmit}>
        <h1>Admin sign in</h1>
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
