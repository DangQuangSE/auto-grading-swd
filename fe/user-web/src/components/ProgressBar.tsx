import { CheckCircle2, Circle, Loader2, XCircle } from "lucide-react";

export type GradingStatus = "Uploaded" | "Extracting" | "AiGrading" | "Completed" | "ExtractionFailed" | "AiGradingFailed" | null;

interface ProgressBarProps {
  status: GradingStatus;
}

export function ProgressBar({ status }: ProgressBarProps) {
  const steps = [
    { key: "Uploaded", label: "File Uploaded" },
    { key: "Extracting", label: "Extracting Artifacts" },
    { key: "AiGrading", label: "AI Grading" },
    { key: "Completed", label: "Completed" },
  ];

  const getStepState = (stepKey: string, index: number) => {
    if (!status) return "pending";

    const currentIndex = steps.findIndex((s) => s.key === status);
    if (currentIndex === -1) {
      if (status === "ExtractionFailed" && index === 1) return "failed";
      if (status === "AiGradingFailed" && index === 2) return "failed";
      
      const failedIndex = status === "ExtractionFailed" ? 1 : status === "AiGradingFailed" ? 2 : -1;
      if (failedIndex !== -1 && index < failedIndex) return "completed";
      return "pending";
    }

    if (index < currentIndex || status === "Completed") return "completed";
    if (index === currentIndex) return "active";
    return "pending";
  };

  return (
    <div style={{ maxWidth: "600px", margin: "0 auto 2rem", display: "flex", justifyContent: "space-between", padding: "1rem", background: "#f8fafc", borderRadius: "0.5rem" }}>
      {steps.map((step, idx) => {
        const state = getStepState(step.key, idx);
        
        return (
          <div key={step.key} style={{ display: "flex", flexDirection: "column", alignItems: "center", flex: 1 }}>
            <div style={{ marginBottom: "0.5rem" }}>
              {state === "completed" && <CheckCircle2 color="#16a34a" />}
              {state === "active" && <Loader2 className="animate-spin" color="#2563eb" />}
              {state === "failed" && <XCircle color="#dc2626" />}
              {state === "pending" && <Circle color="#cbd5e1" />}
            </div>
            <span style={{ 
              fontSize: "0.875rem", 
              fontWeight: state === "active" || state === "failed" ? 600 : 400,
              color: state === "completed" ? "#16a34a" : state === "failed" ? "#dc2626" : state === "active" ? "#2563eb" : "#64748b" 
            }}>
              {step.label}
            </span>
          </div>
        );
      })}
    </div>
  );
}
