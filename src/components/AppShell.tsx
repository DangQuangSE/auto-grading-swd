import { BookOpen, ClipboardCheck, FileUp, GraduationCap, LogOut, UserRound } from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../providers/AuthProvider";

export type AppView = "lecturer" | "rubric" | "student" | "review" | "result";

const navItems: Array<{ to: string; label: string; icon: typeof BookOpen }> = [
  { to: "/dashboard", label: "Dashboard", icon: BookOpen },
  { to: "/rubrics", label: "Rubric", icon: ClipboardCheck },
  { to: "/submit", label: "Submit", icon: FileUp },
  { to: "/review", label: "Review", icon: GraduationCap },
  { to: "/result", label: "Result", icon: UserRound },
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
