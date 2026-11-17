<script lang="ts" setup>
import { computed, reactive } from "vue";
import type { CardEditorExpose } from "@/cards/types.ts";
import ClockCard from "@/cards/ClockCard/ClockCard.vue";
import type { ClockCardConfig, ClockCardVariant } from "@/cards/ClockCard/config.ts";

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<ClockCardConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<ClockCardConfig>({
  variant: props.variant ?? "pixel",
  showSeconds: props.showSeconds ?? false,
  showDate: props.showDate ?? true,
  hour24: props.hour24 ?? true,
  accentColor: props.accentColor ?? "",
});

const variantOptions: Array<{ label: string; value: ClockCardVariant }> = [
  { label: "像素风格", value: "pixel" },
  { label: "手写风格", value: "handwritten" },
  { label: "简约风格", value: "jetbrains" },
];

/** 空值表示跟随卡片文字颜色，选择器用 null 表示 */
const accentColorValue = computed({
  get: () => editValues.accentColor || null,
  set: (value: string | null) => {
    editValues.accentColor = value ?? "";
  },
});

const handleVariantUpdate = (value: string | number) => {
  editValues.variant = value as ClockCardVariant;
};

async function validate(): Promise<boolean> {
  return true;
}

function getConfig() {
  return { ...editValues };
}

defineExpose({ validate, getConfig } satisfies CardEditorExpose);
</script>

<template>
  <n-form :model="editValues" label-placement="top">
    <n-form-item label="预览">
      <div class="clock-editor__preview">
        <ClockCard v-bind="editValues" />
      </div>
    </n-form-item>

    <n-form-item label="风格">
      <n-radio-group :value="editValues.variant" @update:value="handleVariantUpdate">
        <n-radio-button v-for="option in variantOptions" :key="option.value" :value="option.value">
          {{ option.label }}
        </n-radio-button>
      </n-radio-group>
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="显示秒">
          <n-switch v-model:value="editValues.showSeconds" />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="显示日期">
          <n-switch v-model:value="editValues.showDate" />
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="24 小时制">
          <n-switch v-model:value="editValues.hour24" />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="文字颜色">
          <n-color-picker v-model:value="accentColorValue" :show-alpha="false" />
          <n-button quaternary style="margin-left: 12px" @click="editValues.accentColor = ''">
            跟随卡片
          </n-button>
        </n-form-item>
      </n-gi>
    </n-grid>
  </n-form>
</template>

<style scoped>
.clock-editor__preview {
  width: 100%;
  height: 96px;
}
</style>
