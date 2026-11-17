import { ref } from "vue";

/** 与后端 /api/images 的 kind 参数一致 */
export type LibraryKind = "icon" | "image";

export interface LibraryItem {
  id: number;
  originalName: string;
  url: string;
}

export const showLibrary = ref(false);

export const libraryKind = ref<LibraryKind>("icon");

/** 等待选择结果的 resolver，同一时间只允许一个待处理的选择 */
let pendingResolve: ((item: LibraryItem | null) => void) | null = null;

function settle(item: LibraryItem | null) {
  pendingResolve?.(item);
  pendingResolve = null;
  showLibrary.value = false;
}

/** 取消或关闭时返回 null */
export function openImageLibrary(kind: LibraryKind = "icon"): Promise<LibraryItem | null> {
  // 重复打开时先以“取消”结束上一次调用，避免其 Promise 悬挂
  settle(null);

  libraryKind.value = kind;

  return new Promise((resolve) => {
    pendingResolve = resolve;
    showLibrary.value = true;
  });
}

export function selectLibraryItem(item: LibraryItem) {
  settle(item);
}

export function closeImageLibrary() {
  settle(null);
}
