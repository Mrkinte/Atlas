<script lang="ts" setup>
import { nextTick, reactive, ref } from "vue";
import type { InputInst } from "naive-ui";
import type { CardEditorExpose } from "@/cards/types.ts";
import { openImageLibrary } from "@/composables/useImageLibraryHost.ts";
import MarkdownCard from "@/cards/MarkdownCard/MarkdownCard.vue";
import {
  MARKDOWN_CONTENT_MAX_LENGTH,
  MARKDOWN_CONTENT_MAX_WIDTH,
  type MarkdownCardAlign,
  type MarkdownCardConfig,
} from "@/cards/MarkdownCard/config.ts";

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<MarkdownCardConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<MarkdownCardConfig>({
  text: props.text ?? "",
  align: props.align ?? "left",
  maxContentWidth: props.maxContentWidth ?? 0,
  bubbleScroll: props.bubbleScroll ?? true,
});

const mode = ref<"edit" | "preview">("edit");

const inputRef = ref<InputInst | null>(null);

const alignOptions: Array<{ label: string; value: MarkdownCardAlign }> = [
  { label: "左对齐", value: "left" },
  { label: "居中", value: "center" },
];

function handleModeUpdate(value: string | number) {
  mode.value = value as "edit" | "preview";
}

function handleAlignUpdate(value: string | number) {
  editValues.align = value as MarkdownCardAlign;
}

/** 光标处插入 Markdown 图片语法 */
async function handleInsertImage() {
  const item = await openImageLibrary("image");
  if (!item) return;

  const snippet = `![${item.originalName}](${item.url})`;
  const textarea = inputRef.value?.textareaElRef;
  const start = textarea?.selectionStart ?? editValues.text.length;
  const end = textarea?.selectionEnd ?? start;

  editValues.text = editValues.text.slice(0, start) + snippet + editValues.text.slice(end);

  // 等输入框更新完毕再恢复光标，避免被 v-model 重置
  await nextTick();
  const el = inputRef.value?.textareaElRef;
  const caret = start + snippet.length;
  el?.focus();
  el?.setSelectionRange(caret, caret);
}

async function validate(): Promise<boolean> {
  return true;
}

function getConfig(): MarkdownCardConfig {
  const width = Number(editValues.maxContentWidth);

  return {
    text: editValues.text,
    align: editValues.align,
    // 输入框清空时值为 null，收敛到合法区间
    maxContentWidth: Number.isFinite(width)
      ? Math.min(MARKDOWN_CONTENT_MAX_WIDTH, Math.max(0, Math.round(width)))
      : 0,
    bubbleScroll: editValues.bubbleScroll,
  };
}

defineExpose({ validate, getConfig } satisfies CardEditorExpose);
</script>

<template>
  <n-form :model="editValues" label-placement="top">
    <n-form-item label="内容">
      <n-flex :size="10" style="width: 100%" vertical>
        <n-radio-group :value="mode" @update:value="handleModeUpdate">
          <n-radio-button value="edit">编辑</n-radio-button>
          <n-radio-button value="preview">预览</n-radio-button>
        </n-radio-group>

        <n-input
          v-if="mode === 'edit'"
          ref="inputRef"
          v-model:value="editValues.text"
          :maxlength="MARKDOWN_CONTENT_MAX_LENGTH"
          :rows="12"
          placeholder="请输入 Markdown 内容"
          show-count
          type="textarea"
        />
        <div v-else class="markdown-editor__preview">
          <MarkdownCard v-bind="editValues" />
        </div>

        <n-flex align="center" justify="space-between">
          <n-button v-if="mode === 'edit'" @click="handleInsertImage"> 插入图片 </n-button>
          <n-text depth="3" style="font-size: 12px">
            支持标题、列表、表格、任务列表、代码块、链接等语法
          </n-text>
        </n-flex>
      </n-flex>
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="对齐方式">
          <n-radio-group :value="editValues.align" @update:value="handleAlignUpdate">
            <n-radio-button
              v-for="option in alignOptions"
              :key="option.value"
              :value="option.value"
            >
              {{ option.label }}
            </n-radio-button>
          </n-radio-group>
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="内容最大宽度（0 不限）">
          <n-input-number
            v-model:value="editValues.maxContentWidth"
            :max="MARKDOWN_CONTENT_MAX_WIDTH"
            :min="0"
            :step="20"
            style="width: 100%"
          >
            <template #suffix>px</template>
          </n-input-number>
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-form-item label="滚动冒泡到首页">
      <n-flex :size="12" align="center">
        <n-switch v-model:value="editValues.bubbleScroll" />
        <n-text depth="3" style="font-size: 12px">
          卡片内容滚到边界后继续滚动首页
        </n-text>
      </n-flex>
    </n-form-item>
  </n-form>
</template>

<style scoped>
.markdown-editor__preview {
  box-sizing: border-box;
  width: 100%;
  height: 320px;
  border: 1px solid rgba(128, 128, 128, 0.25);
  border-radius: 6px;
}
</style>
