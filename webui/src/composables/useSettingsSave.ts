import { useAppMessage } from "@/composables/useAppMessage.ts";
import type { Settings } from "@/config/default.ts";
import { saveSettings } from "@/stores/settings.ts";

/** 失败时提示，由调用方通过 onError 决定是否回退界面 */
export function useSettingsSave(onError?: () => void) {
  const message = useAppMessage();

  return async function saveSiteSettings(patch: Partial<Settings>): Promise<string | null> {
    const error = await saveSettings(patch);
    if (error) {
      message.error(error);
      onError?.();
    }
    return error;
  };
}
