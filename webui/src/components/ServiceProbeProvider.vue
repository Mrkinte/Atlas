<script lang="ts" setup>
import { computed, provide } from "vue";
import { SERVICE_PROBE_KEY } from "@/composables/serviceProbeContext.ts";
import type { ServiceProbeEntry } from "@/services/serviceProbeScheduler.ts";

const props = defineProps<{
  entry?: ServiceProbeEntry;
}>();

// 注入 computed：探测结果更新时，卡片内的读取保持响应式
provide(
  SERVICE_PROBE_KEY,
  computed(() => props.entry),
);
</script>

<template>
  <slot />
</template>
