import { FileUp, GraduationCap, LogOut, UserRound } from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../providers/AuthProvider";
import { courseEnrollmentEnabled } from "../lib/features";

export type AppView = "student" | "result";

const navItems: Array<{ to: string; label: string; icon: typeof FileUp }> = [
  { to: "/submit", label: "Submit", icon: FileUp },
  { to: "/result", label: "Result", icon: UserRound },
  { to: "/profile", label: "Profile", icon: UserRound },
];

export function AppShell() {
  const { session, signOutUser } = useAuth();

  return (
    <div className="app-frame">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <GraduationCap aria-hidden="true" />
          <span>Auto Grading</span>
        </div>
        <nav className="sidebar-nav" aria-label="Main navigation">
          {navItems.filter((item) => item.to !== "/profile" || courseEnrollmentEnabled).map((item) => {
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
          <button className="nav-button" type="button" onClick={signOutUser} title="Sign out">
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
