import type { Metadata } from "next";

// Auth pages (login, confirm, reset) have no search value and shouldn't be indexed
export const metadata: Metadata = {
  title: "Sign in",
  robots: { index: false, follow: false },
};

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return children;
}
