import { LockKeyhole } from "lucide-react";

export function LoginPage() {
  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Access</p>
        <h1>Sign in</h1>
      </header>
      <form className="form-panel">
        <label>
          Email
          <input type="email" placeholder="lecturer@school.edu" />
        </label>
        <label>
          Password
          <input type="password" placeholder="••••••••" />
        </label>
        <button className="primary-button" type="button">
          <LockKeyhole aria-hidden="true" />
          Sign in
        </button>
      </form>
    </section>
  );
}
