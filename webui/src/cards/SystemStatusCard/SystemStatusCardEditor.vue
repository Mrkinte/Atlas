<script lang="ts" setup>
import { reactive } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import type { CardEditorExpose } from "@/cards/types.ts";
import SystemStatusCard from "@/cards/SystemStatusCard/SystemStatusCard.vue";
import type { SystemStatusCardConfig } from "@/cards/SystemStatusCard/config.ts";
import { SYSTEM_STATUS_VARIANTS } from "@/cards/SystemStatusCard/config.ts";

const message = useAppMessage();

type ToggleKey = "showMemory" | "showDisk" | "showLoad" | "showProcess" | "showUptime";

const toggles: Array<{ label: string; key: ToggleKey }> = [
  { label: "内存占用", key: "showMemory" },
  { label: "磁盘占用", key: "showDisk" },
  { label: "系统负载", key: "showLoad" },
  { label: "进程内存", key: "showProcess" },
  { label: "运行时间", key: "showUptime" },
];

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<SystemStatusCardConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<SystemStatusCardConfig>({
  title: props.title ?? "系统状态",
  variant: props.variant ?? "classic",
  showMemory: props.showMemory ?? true,
  showDisk: props.showDisk ?? true,
  showLoad: props.showLoad ?? true,
  showProcess: props.showProcess ?? true,
  showUptime: props.showUptime ?? true,
});

function setToggle(key: ToggleKey, value: boolean) {
  editValues[key] = value;
}

async function validate(): Promise<boolean> {
  if (!editValues.title.trim()) {
    message.warning("请输入卡片标题");
    return false;
  }

  return true;
}

function getConfig() {
  return { ...editValues, title: editValues.title.trim() };
}

defineExpose({ validate, getConfig } satisfies CardEditorExpose);
</script>

<template>
  <n-form :model="editValues" label-placement="top">
    <n-form-item label="预览">
      <div class="status-editor__preview">
        <SystemStatusCard v-bind="editValues" />
      </div>
    </n-form-item>

    <n-form-item label="标题">
      <n-input v-model:value="editValues.title" :maxlength="24" placeholder="系统状态" show-count />
    </n-form-item>

    <n-form-item label="外观样式">
      <n-radio-group v-model:value="editValues.variant">
        <n-radio-button
          v-for="item in SYSTEM_STATUS_VARIANTS"
          :key="item.value"
          :value="item.value"
        >
          {{ item.label }}
        </n-radio-button>
      </n-radio-group>
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi v-for="item in toggles" :key="item.key">
        <n-form-item :label="item.label">
          <n-switch :value="editValues[item.key]" @update:value="setToggle(item.key, $event)" />
        </n-form-item>
      </n-gi>
    </n-grid>
  </n-form>
</template>

<style scoped>
.status-editor__preview {
  width: 100%;
  height: 142px;
}
</style>
