<script lang="ts" setup>
import type { WelcomeCardConfig } from "@/cards/WelcomeCard/config.ts";

/** 历史数据可能缺字段，全部可选并由默认值补齐 */
const props = withDefaults(defineProps<Partial<WelcomeCardConfig>>(), {
  title: "",
  subtitle: "",
  titleColor: "",
  subtitleColor: "",
  align: "left",
});
</script>

<template>
  <div :style="{ textAlign: props.align }" class="welcome">
    <div :style="{ color: props.titleColor || undefined }" class="welcome__title">
      {{ props.title }}
    </div>
    <p :style="{ color: props.subtitleColor || undefined }" class="welcome__subtitle">
      {{ props.subtitle }}
    </p>
  </div>
</template>

<style scoped>
.welcome {
  display: flex;
  flex-direction: column;
}

.welcome__title {
  font-size: 1.6rem;
}

.welcome__subtitle {
  margin: 4px 0;
}
</style>
