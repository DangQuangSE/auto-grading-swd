import { useAuth } from "../providers/AuthProvider";
import { Panel } from "../components/ui/Panel";

export function DashboardPage() {
  const { session } = useAuth();

  return (
    <section className="page-grid">
      <header className="page-header">
        <p>Admin</p>
        <h1>Dashboard</h1>
      </header>
      <Panel className="compact-page">
        <p>Signed in as {session?.user.email}.</p>
      </Panel>
    </section>
  );
}
