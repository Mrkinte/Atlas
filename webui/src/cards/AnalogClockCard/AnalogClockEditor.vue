<script lang="ts" setup>
import { computed, reactive } from "vue";
import type { CardEditorExpose } from "@/cards/types.ts";
import AnalogClock from "@/cards/AnalogClockCard/AnalogClock.vue";
import type { AnalogClockConfig } from "@/cards/AnalogClockCard/config.ts";

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<AnalogClockConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<AnalogClockConfig>({
  showSeconds: props.showSeconds ?? true,
  smoothSeconds: props.smoothSeconds ?? false,
  showNumbers: props.showNumbers ?? false,
  accentColor: props.accentColor ?? "",
  backgroundOpacity: props.backgroundOpacity ?? 100,
});

const formatOpacityTooltip = (value: number) => `${value}%`;

/** 空值表示跟随默认色，选择器用 null 表示 */
const accentColorValue = computed({
  get: () => editValues.accentColor || null,
  set: (value: string | null) => {
    editValues.accentColor = value ?? "";
  },
});

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
      <div class="analog-editor__preview">
        <AnalogClock v-bind="editValues" />
      </div>
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="显示秒针">
          <n-switch v-model:value="editValues.showSeconds" />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="秒针连续扫动">
          <n-switch v-model:value="editValues.smoothSeconds" :disabled="!editValues.showSeconds" />
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="显示表盘数字">
          <n-switch v-model:value="editValues.showNumbers" />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="秒针颜色">
          <n-color-picker v-model:value="accentColorValue" :show-alpha="false" />
          <n-button quaternary style="margin-left: 12px" @click="editValues.accentColor = ''">
            跟随默认
          </n-button>
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-form-item label="背景不透明度">
      <n-slider
        v-model:value="editValues.backgroundOpacity"
        :format-tooltip="formatOpacityTooltip"
        :max="100"
        :min="0"
        :step="5"
      />
    </n-form-item>
  </n-form>
</template>

<style scoped>
.analog-editor__preview {
  width: 100%;
  height: 180px;
}
</style>
