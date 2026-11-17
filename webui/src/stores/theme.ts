import { computed, ref } from "vue";
import { darkTheme, lightTheme } from "naive-ui";
import type { BuiltInGlobalTheme } from "naive-ui/es/themes/interface";

const THEME_STORAGE_KEY = "theme";

export type ThemeMode = "system" | "light" | "dark";

const THEME_MODES: ThemeMode[] = ["system", "light", "dark"];

function readStoredMode(): ThemeMode {
  const saved = localStorage.getItem(THEME_STORAGE_KEY);
  return saved && THEME_MODES.includes(saved as ThemeMode) ? (saved as ThemeMode) : "system";
}

export const themeMode = ref<ThemeMode>(readStoredMode());

const systemPrefersDark = ref(false);

const darkSchemeQuery = window.matchMedia?.("(prefers-color-scheme: dark)");

if (darkSchemeQuery) {
  systemPrefersDark.value = darkSchemeQuery.matches;
  darkSchemeQuery.addEventListener("change", (event) => {
    systemPrefersDark.value = event.matches;
  });
}

export const appTheme = computed<BuiltInGlobalTheme>(() => {
  const dark = themeMode.value === "system" ? systemPrefersDark.value : themeMode.value === "dark";

  return dark ? darkTheme : lightTheme;
});

export function setThemeMode(mode: ThemeMode) {
  themeMode.value = mode;
  localStorage.setItem(THEME_STORAGE_KEY, mode);
}
