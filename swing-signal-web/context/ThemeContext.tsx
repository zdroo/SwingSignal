"use client";

import { createContext, useCallback, useContext, useEffect, useState } from "react";

type Theme = "light" | "dark";

interface ThemeContextValue {
  theme: Theme;
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  // Default dark — it's the product's native look; the FOUC script in layout
  // has already stamped the class on <html> before hydration.
  const [theme, setTheme] = useState<Theme>("dark");

  // Deliberate post-mount hydration: reading localStorage in a lazy
  // initializer would make the first client render differ from the
  // server-rendered HTML and trip React's hydration mismatch.
  useEffect(() => {
    const stored = localStorage.getItem("ss_theme") as Theme | null;
    // eslint-disable-next-line react-hooks/set-state-in-effect -- SSR-safe hydration, see above
    if (stored === "light" || stored === "dark") setTheme(stored);
  }, []);

  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
    localStorage.setItem("ss_theme", theme);
  }, [theme]);

  const toggleTheme = useCallback(() => {
    setTheme((t) => (t === "dark" ? "light" : "dark"));
  }, []);

  return (
    <ThemeContext.Provider value={{ theme, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
