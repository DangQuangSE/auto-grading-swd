export function FormMessage({ tone, children }: { tone: "error" | "success"; children: string }) {
  return <p className={tone === "error" ? "form-error" : "form-message"}>{children}</p>;
}
