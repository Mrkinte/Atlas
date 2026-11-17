import { type Component, markRaw, ref, shallowRef } from "vue";

/** 保存回调：由调用方决定如何持久化，抛错表示失败 */
export type EditorSaveHandler = (data: Record<string, unknown>) => void | Promise<void>;

export interface EditorOpenOptions {
  props?: Record<string, unknown>;
  onSave?: EditorSaveHandler;
}

/** 当前挂载的编辑器组件，由 CardEditorHost 渲染 */
const editorComponent = shallowRef<Component | null>(null);
const editorVisible = ref(false);
const editorProps = ref<Record<string, unknown>>({});
/** 非响应式，仅在保存时读取 */
let editorOnSave: EditorSaveHandler | null = null;

export function openEditor(editor?: Component, options: EditorOpenOptions = {}): boolean {
  if (!editor) {
    console.warn(`[cardEditorHost] 未找到编辑器组件`);
    closeEditor();
    return false;
  }

  editorComponent.value = markRaw(editor);
  editorProps.value = {
    ...options.props,
  };
  editorOnSave = options.onSave ?? null;
  editorVisible.value = true;
  return true;
}

function closeEditor() {
  editorVisible.value = false;
}

/** 等关闭动画结束后清理 */
function cleanEditor() {
  editorComponent.value = null;
  editorProps.value = {};
  editorOnSave = null;
}

export function useCardEditorHost() {
  return {
    editorComponent,
    editorVisible,
    editorProps,
    closeEditor,
    cleanEditor,
    /** 取本次编辑的保存回调（函数形式，避免解构后拿到旧值） */
    getEditorOnSave: () => editorOnSave,
  };
}
