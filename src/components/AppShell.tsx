import { BookOpen, ClipboardCheck, FileUp, GraduationCap, UserRound } from "lucide-react";
import type { ReactNode } from "react";

export type AppView = "lecturer" | "rubric" | "student" | "review" | "result";

const navItems: Array<{ id: AppView; label: string; icon: typeof BookOpen }> = [
  { id: "lecturer", label: "Dashboard", icon: BookOpen },
  { id: "rubric", label: "Rubric", icon: ClipboardCheck },
  { id: "student", label: "Submit", icon: FileUp },
  { id: "review", label: "Review", icon: GraduationCap },
  { id: "result", label: "Result", icon: UserRound },
];

export function AppShell({
  activeView,
  onViewChange,
  children,
}: {
  activeView: AppView;
  onViewChange: (view: AppView) => void;
  children: ReactNode;
}) {
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
              <button
                key={item.id}
                className={activeView === item.id ? "nav-button active" : "nav-button"}
                type="button"
                onClick={() => onViewChange(item.id)}
                title={item.label}
              >
                <Icon aria-hidden="true" />
                <span>{item.label}</span>
              </button>
            );
          })}
        </nav>
      </aside>
      <section className="workspace">{children}</section>
    </div>
  );
}
