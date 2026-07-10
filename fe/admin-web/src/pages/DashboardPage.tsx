import { useAuth } from "../providers/AuthProvider";

export function DashboardPage() {
  const { session, signOut } = useAuth();

  return (
    <section className="dashboard-page">
      <header className="dashboard-header">
        <h1>Admin dashboard</h1>
        <button type="button" onClick={signOut}>
          Sign out
        </button>
      </header>
      <p>Signed in as {session?.user.email}.</p>
    </section>
  );
}
