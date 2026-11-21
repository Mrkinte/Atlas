<script lang="ts" setup>
import { reactive } from "vue";
import type { CardEditorExpose } from "@/cards/types.ts";
import IpCard from "@/cards/IpCard/IpCard.vue";
import type { IpCardConfig } from "@/cards/IpCard/config.ts";

type ToggleKey = "showLocalIp" | "showIsp" | "showLocation" | "showLatency";

const toggles: Array<{ label: string; key: ToggleKey }> = [
  { label: "内网 IP", key: "showLocalIp" },
  { label: "运营商", key: "showIsp" },
  { label: "位置", key: "showLocation" },
  { label: "平均延迟", key: "showLatency" },
];

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<IpCardConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<IpCardConfig>({
  showLocalIp: props.showLocalIp ?? true,
  showIsp: props.showIsp ?? true,
  showLocation: props.showLocation ?? true,
  showLatency: props.showLatency ?? true,
});

function setToggle(key: ToggleKey, value: boolean) {
  editValues[key] = value;
}

async function validate(): Promise<boolean> {
  return true;
}

function getConfig() {
  return { ...editValues};
}

defineExpose({ validate, getConfig } satisfies CardEditorExpose);
</script>

<template>
  <n-form :model="editValues" label-placement="top">
    <n-form-item label="预览">
      <div class="ip-editor__preview">
        <IpCard v-bind="editValues" />
      </div>
    </n-form-item>
    
    <n-grid :cols="2">
      <n-gi v-for="item in toggles" :key="item.key">
        <n-form-item :label="item.label">
          <n-switch :value="editValues[item.key]" @update:value="setToggle(item.key, $event)" />
        </n-form-item>
      </n-gi>
    </n-grid>
  </n-form>
</template>

<style scoped>
.ip-editor__preview {
  width: 100%;
  height: 232px;
}
</style>
