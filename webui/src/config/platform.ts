import type { Platform } from "@/config/default.ts";
import { DESKTOP_GRID_COLS } from "@/config/default.ts";
import { desktopGridContainerMinWidth } from "@/utils/cardLayoutCalculate.ts";
import { ref } from "vue";

export const platform = ref<Platform>("desktop");

/** 断点取 12 列桌面栅格所需的最小宽度 */
const MOBILE_MAX_WIDTH = desktopGridContainerMinWidth(DESKTOP_GRID_COLS);

function getDeviceQuery(): string {
  return `(max-width: ${MOBILE_MAX_WIDTH}px)`;
}

let manualOverride = false;
let lastPlatform: Platform = platform.value;

export function enableSimulateMobilePlatform() {
  manualOverride = true;
  platform.value = "mobile";
}

export function disableSimulateMobilePlatform() {
  manualOverride = false;
  platform.value = lastPlatform;
}

export function watchDevicePlatform(): () => void {
  const media = window.matchMedia(getDeviceQuery());

  const apply = (matches: boolean) => {
    if (manualOverride) return;
    platform.value = matches ? "mobile" : "desktop";
    lastPlatform = platform.value;
  };

  const onChange = (event: MediaQueryListEvent) => apply(event.matches);
  media.addEventListener("change", onChange);

  // 首次订阅即按当前视口初始化，避免手机上停留在默认的 desktop
  apply(media.matches);

  return () => media.removeEventListener("change", onChange);
}
