import { onBeforeUnmount } from "vue";
import { settings } from "@/stores/settings.ts";
import { useSettingsSave } from "@/composables/useSettingsSave.ts";

export type CommitSliderKey = "backgroundBlur" | "backgroundMask";

function listenPointerUp(commit: () => void) {
  window.removeEventListener("pointerup", commit);
  window.removeEventListener("pointercancel", commit);
  window.addEventListener("pointerup", commit);
  window.addEventListener("pointercancel", commit);
}

function unlistenPointerUp(commit: () => void) {
  window.removeEventListener("pointerup", commit);
  window.removeEventListener("pointercancel", commit);
}

/** 拖动时实时改 store 预览，松手后才写服务端 */
export function useCommitSlider(key: CommitSliderKey) {
  const saveSiteSettings = useSettingsSave();

  function handleInput(value: number) {
    settings[key] = value;
  }

  function handleCommit() {
    unlistenPointerUp(handleCommit);
    void saveSiteSettings({ [key]: settings[key] } as Partial<typeof settings>);
  }

  function handlePointerDown() {
    listenPointerUp(handleCommit);
  }

  onBeforeUnmount(() => unlistenPointerUp(handleCommit));

  return { handleInput, handlePointerDown, handleCommit };
}
