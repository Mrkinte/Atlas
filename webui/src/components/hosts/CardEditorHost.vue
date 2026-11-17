<script lang="ts" setup>
import { useCardEditorHost } from "@/composables/useCardEditorHost.ts";
import { ref, watch } from "vue";
import type { CardEditorExpose } from "@/cards/types.ts";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { SETTINGS_SAVE_FAILED } from "@/config/messages.ts";

const message = useAppMessage();
const { editorComponent, editorVisible, editorProps, closeEditor, cleanEditor, getEditorOnSave } =
  useCardEditorHost();

const saving = ref(false);
const editorRef = ref<CardEditorExpose | null>(null);

watch(editorVisible, (v) => {
  // 关闭时清空实例引用，避免下次打开短暂拿到旧组件
  if (!v) editorRef.value = null;
});

async function handleSave() {
  const editor = editorRef.value;
  if (!editor || saving.value) return;

  saving.value = true;
  try {
    const ok = await editor.validate();
    if (!ok) return;

    await getEditorOnSave()?.(editor.getConfig());

    message.success("保存成功");
    closeEditor();
  } catch (err) {
    message.error((err as Error)?.message ?? SETTINGS_SAVE_FAILED);
  } finally {
    saving.value = false;
  }
}

/** 保存过程中禁止关闭，避免请求还在飞时丢失上下文 */
function handleShowChange(visible: boolean) {
  if (!visible && !saving.value) closeEditor();
}
</script>

<template>
  <Teleport to="body">
    <n-modal
      :close-on-esc="!saving"
      :content-style="{ minHeight: '0', overflowY: 'auto' }"
      :mask-closable="!saving"
      :show="editorVisible"
      draggable
      preset="card"
      style="max-width: var(--editor-max-width); max-height: calc(100vh - 32px)"
      title="编辑"
      @update:show="handleShowChange"
      @after-leave="cleanEditor"
    >
      <component :is="editorComponent" ref="editorRef" v-bind="editorProps" />

      <template #footer>
        <n-flex justify="end">
          <n-button :disabled="saving" @click="closeEditor">取消</n-button>
          <n-button :loading="saving" type="primary" @click="handleSave"> 保存 </n-button>
        </n-flex>
      </template>
    </n-modal>
  </Teleport>
</template>

<style scoped></style>
