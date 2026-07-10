import type { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonVariant = "primary" | "secondary" | "text";

export function Button({
  variant = "primary",
  children,
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  children: ReactNode;
}) {
  const variantClass = {
    primary: "primary-button",
    secondary: "secondary-button",
    text: "text-button",
  }[variant];

  return (
    <button className={[variantClass, className].filter(Boolean).join(" ")} {...props}>
      {children}
    </button>
  );
}
