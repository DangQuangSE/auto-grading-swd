import {
  ClipboardCheck,
  ClipboardList,
  FileSpreadsheet,
  GraduationCap,
  LayoutDashboard,
  Library,
  LogOut,
  ShieldCheck,
  UserCheck,
  Users,
} from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../providers/AuthProvider";
import { courseEnrollmentEnabled } from "../lib/features";

const navItems: Array<{ to: string; label: string; icon: typeof LayoutDashboard }> = [
  { to: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { to: "/subjects", label: "Subjects", icon: Library },
  { to: "/assignments", label: "Assignments", icon: ClipboardList },
  { to: "/rubrics", label: "Rubric", icon: ClipboardCheck },
  { to: "/classes", label: "Classes", icon: Users },
  { to: "/roster", label: "Roster", icon: UserCheck },
  { to: "/grades", label: "Grades", icon: FileSpreadsheet },
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
          {navItems.filter((item) => item.to !== "/classes" || (courseEnrollmentEnabled && session?.user.role === "admin")).map((item) => {
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
