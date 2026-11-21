<script lang="ts" setup>
import { reactive } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import type { CardEditorExpose } from "@/cards/types.ts";
import DockerCard from "@/cards/DockerCard/DockerCard.vue";
import type { DockerCardConfig } from "@/cards/DockerCard/config.ts";

const message = useAppMessage();

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<DockerCardConfig>>();

const editValues = reactive<DockerCardConfig>({
  title: props.title ?? "Docker",
});

async function validate() {
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
      <div style="width: 100%; height: 240px">
        <DockerCard v-bind="editValues" />
      </div>
    </n-form-item>

    <n-form-item label="标题">
      <n-input v-model:value="editValues.title" :maxlength="24" placeholder="Docker" show-count />
    </n-form-item>
  </n-form>
</template>
