<script lang="ts" setup>
import { computed } from "vue";
import type { CardPlugin } from "@/cards/types.ts";
import { listCardPlugins } from "@/cards/registry.ts";

const props = defineProps<{ show: boolean }>();

const emit = defineEmits<{
  "update:show": [value: boolean];
  select: [cardPlugin: CardPlugin];
}>();

const handleClose = () => emit("update:show", false);

/** 桌面端固定两列 */
const PREVIEW_COLUMNS = 2;

/** 宽度由插件的 previewSpan 决定，未声明按一列，最多两列 */
const previewSpan = (cardPlugin: CardPlugin) =>
  Math.min(Math.max(cardPlugin.previewSpan ?? 1, 1), PREVIEW_COLUMNS);

/**
 * 从宽到窄排列：宽卡片先占满一行，窄卡片再两两成对，
 * 避免窄卡片后面紧跟宽卡片时被迫换行留下空位；sort 稳定，同宽保持注册顺序
 */
const cardOptions = computed(() =>
  [...listCardPlugins()].sort((a, b) => previewSpan(b) - previewSpan(a)),
);

function handleSelect(option: CardPlugin) {
  emit("select", option);
  handleClose();
}
</script>

<template>
  <n-modal
    :content-style="{ minHeight: '0' }"
    :show="props.show"
    preset="card"
    style="width: auto; max-width: calc(100vw - 32px); max-height: calc(100vh - 32px)"
    title="添加卡片"
    @update:show="handleClose"
    @mask-click="handleClose"
  >
    <n-scrollbar style="max-height: calc(100vh - 120px)">
      <div class="card-grid">
        <n-card
          v-for="cardPlugin in cardOptions"
          :key="cardPlugin.name"
          :content-style="{
            pointerEvents: 'none',
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'center',
            gap: '8px',
            minHeight: '64px',
          }"
          :contentScrollable="true"
          :style="{
            cursor: 'pointer',
            '--preview-span': previewSpan(cardPlugin),
          }"
          class="card-option"
          hoverable
          @click="handleSelect(cardPlugin)"
        >
          <div class="card-option__preview">
            <component :is="cardPlugin.display" v-bind="cardPlugin.defaultConfig" />
          </div>
          <span v-if="cardPlugin.label" class="card-option__label">
            {{ cardPlugin.label }}
          </span>
        </n-card>
      </div>
    </n-scrollbar>
  </n-modal>
</template>

<style scoped>
.card-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  grid-auto-flow: row dense;
  gap: 12px;
  width: 640px;
  max-width: 100%;
  box-sizing: border-box;
}

.card-option {
  /* --preview-span 由模板注入，默认一列 */
  grid-column: span var(--preview-span, 1);
}

.card-option__preview {
  width: 100%;
}

.card-option__label {
  font-size: 12px;
  line-height: 1.4;
  opacity: 0.65;
  text-align: center;
}

@media (max-width: 640px) {
  .card-grid {
    grid-template-columns: minmax(0, 1fr);
    width: 100%;
    gap: 10px;
  }

  .card-option {
    grid-column: auto;
  }
}
</style>
