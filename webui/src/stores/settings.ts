import { reactive } from "vue";
import { getSettings, updateSettings } from "@/apis/modules/settings.ts";
import {
  DEFAULT_SETTINGS,
  FOOTER_TEXT_MAX_LENGTH,
  type Settings,
  SETTINGS_LIMITS,
  SITE_SUBTITLE_MAX_LENGTH,
  SITE_TITLE_MAX_LENGTH,
} from "@/config/default.ts";
import { NETWORK_ERROR, SETTINGS_SAVE_FAILED } from "@/config/messages.ts";
import { demoMode } from "@/stores/demo.ts";
import { normalizeHexColor } from "@/utils/colorConverter.ts";

/** 全局单例，初始值为后端不可用时的兜底 */
export const settings = reactive<Settings>({
  ...DEFAULT_SETTINGS,
});

function clampInt(
  value: unknown,
  limits: { readonly min: number; readonly max: number },
  fallback: number,
): number {
  if (typeof value !== "number" || !Number.isFinite(value)) return fallback;
  return Math.min(limits.max, Math.max(limits.min, Math.round(value)));
}

function normalizeTitle(value: unknown): string {
  if (typeof value !== "string") return DEFAULT_SETTINGS.siteTitle;
  const title = value.trim().slice(0, SITE_TITLE_MAX_LENGTH);
  return title || DEFAULT_SETTINGS.siteTitle;
}

function normalizeBackgroundImage(value: unknown): string {
  return typeof value === "string" ? value.trim().slice(0, 1024) : "";
}

function normalizeText(value: unknown, max: number): string {
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

/** 仅接受站内相对路径或 http(s) 地址 */
function normalizeFavicon(value: unknown): string {
  if (typeof value !== "string") return "";
  const url = value.trim().slice(0, 1024);
  if (url.startsWith("/")) return url;
  if (url.startsWith("http://") || url.startsWith("https://")) return url;
  return "";
}

/** 仅接受内置或上传目录下的 .model3.json */
function normalizeLive2dModelPath(value: unknown): string {
  if (typeof value !== "string") return "";
  const path = value.trim().slice(0, 1024);
  if (!path.toLowerCase().endsWith(".model3.json")) return "";
  if (path.startsWith("/live2d/") || path.startsWith("/uploads/live2d/")) {
    return path;
  }
  return "";
}

export function normalizeSettings(input: Partial<Record<keyof Settings, unknown>>): Settings {
  return {
    desktopCols: clampInt(
      input.desktopCols,
      SETTINGS_LIMITS.desktopCols,
      DEFAULT_SETTINGS.desktopCols,
    ),
    desktopGridContainerMaxWidth: clampInt(
      input.desktopGridContainerMaxWidth,
      SETTINGS_LIMITS.desktopGridContainerMaxWidth,
      DEFAULT_SETTINGS.desktopGridContainerMaxWidth,
    ),
    desktopGridFullWidth: input.desktopGridFullWidth === true,
    siteTitle: normalizeTitle(input.siteTitle),
    siteSubtitle: normalizeText(input.siteSubtitle, SITE_SUBTITLE_MAX_LENGTH),
    favicon: normalizeFavicon(input.favicon),
    showHeaderLogo: input.showHeaderLogo !== false,
    footerText: normalizeText(input.footerText, FOOTER_TEXT_MAX_LENGTH),
    backgroundImage: normalizeBackgroundImage(input.backgroundImage),
    backgroundBlur: clampInt(
      input.backgroundBlur,
      SETTINGS_LIMITS.backgroundBlur,
      DEFAULT_SETTINGS.backgroundBlur,
    ),
    backgroundMask: clampInt(
      input.backgroundMask,
      SETTINGS_LIMITS.backgroundMask,
      DEFAULT_SETTINGS.backgroundMask,
    ),
    primaryColor: normalizeHexColor(input.primaryColor),
    publicHardware: normalizePublicHardware(input.publicHardware),
    systemStatusEnabled: input.systemStatusEnabled !== false,
    serviceProbeEnabled: input.serviceProbeEnabled !== false,
    live2DEnabled: input.live2DEnabled === true,
    live2DModelPath: normalizeLive2dModelPath(input.live2DModelPath),
  };
}

function normalizePublicHardware(value: unknown): string[] {
  if (!Array.isArray(value)) return [];

  const result: string[] = [];
  for (const item of value) {
    if (typeof item !== "string") continue;

    const id = item.trim();
    // 刻意匹配控制字符用于拒绝非法输入，并非误用正则
    // eslint-disable-next-line no-control-regex
    if (!id || id.length > 128 || /[\u0000-\u001f\u007f]/.test(id)) continue;
    if (result.includes(id)) continue;

    result.push(id);
    if (result.length >= 32) break;
  }

  return result;
}

function applySettings(input: Partial<Record<keyof Settings, unknown>>) {
  Object.assign(settings, normalizeSettings(input));
}

let firstLoadTask: Promise<void> | undefined;

/** 接口异常时保持默认值，不阻塞页面 */
export function loadSettings(): Promise<void> {
  // 演示模式的站点配置只在启动时读取一次
  if (firstLoadTask && demoMode.value) return firstLoadTask;

  const task = fetchSettings();
  firstLoadTask ??= task;
  return task;
}

/** 看板娘据此决定初始模型 */
export function whenSettingsLoaded(): Promise<void> {
  return firstLoadTask ?? loadSettings();
}

async function fetchSettings() {
  try {
    const { data, response } = await getSettings();
    if (!response.ok) return;
    applySettings(data as Record<string, unknown>);
  } catch {
    // 首次加载失败时保持默认值，不阻塞页面
  }
}

/** 成功返回 null，失败返回提示文案 */
export async function saveSettings(patch: Partial<Settings>): Promise<string | null> {
  const next = normalizeSettings({ ...settings, ...patch });

  if (demoMode.value) {
    applySettings(next);
    return null;
  }

  try {
    const { response } = await updateSettings(next);
    if (!response.ok) return SETTINGS_SAVE_FAILED;
    applySettings(next);
    return null;
  } catch {
    return NETWORK_ERROR;
  }
}
