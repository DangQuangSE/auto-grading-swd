import { ClipboardCheck, GraduationCap, LayoutDashboard, Library, LogOut, ShieldCheck } from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../providers/AuthProvider";

const navItems: Array<{ to: string; label: string; icon: typeof LayoutDashboard }> = [
  { to: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { to: "/subjects", label: "Subjects", icon: Library },
  { to: "/rubrics", label: "Rubric", icon: ClipboardCheck },
  { to: "/review", label: "Review", icon: GraduationCap },
];

export function AppShell() {
  const { session, signOut } = useAuth();

  return (
    <div className="app-frame">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <ShieldCheck aria-hidden="true" />
          <span>Auto Grading Admin</span>
        </div>
        <nav className="sidebar-nav" aria-label="Main navigation">
          {navItems.map((item) => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.to}
                className={({ isActive }) => (isActive ? "nav-button active" : "nav-button")}
                to={item.to}
                title={item.label}
              >
                <Icon aria-hidden="true" />
                <span>{item.label}</span>
              </NavLink>
            );
          })}
        </nav>
        <div className="sidebar-account">
          <span>{session?.user.email ?? "Signed in"}</span>
          <button className="nav-button" type="button" onClick={signOut} title="Sign out">
            <LogOut aria-hidden="true" />
            <span>Sign out</span>
          </button>
        </div>
      </aside>
      <section className="workspace">
        <Outlet />
      </section>
    </div>
  );
}
