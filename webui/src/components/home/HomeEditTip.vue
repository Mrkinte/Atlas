<script lang="ts" setup>
import { ref } from "vue";

const dismissed = ref(false);
</script>

<template>
  <n-alert
    v-if="!dismissed"
    class="edit-tip"
    closable
    show-icon
    title="操作提示"
    type="info"
    @close="dismissed = true"
  >
    <n-flex :size="4" vertical>
      <n-text depth="3">1. 按住 Ctrl 拖动卡片即可复制</n-text>
      <n-text depth="3">2. 拖动卡片右下角边缘即可调整大小</n-text>
      <n-text depth="3">3. 右键卡片或空白处打开更多操作</n-text>
    </n-flex>
  </n-alert>

  <n-tooltip v-else placement="right">
    <template #trigger>
      <n-button
        class="edit-tip__handle"
        secondary
        size="small"
        type="info"
        @click="dismissed = false"
      >
        操作提示
      </n-button>
    </template>
    显示操作提示
  </n-tooltip>
</template>

<style scoped>
.edit-tip {
  position: absolute;
  top: 24px;
  left: 24px;
  z-index: 10;
  max-width: 300px;
}

.edit-tip__handle {
  position: absolute;
  top: 24px;
  left: 0;
  z-index: 10;
  height: auto;
  padding: 12px 5px;
  letter-spacing: 2px;
  writing-mode: vertical-rl;
  border-radius: 0 var(--border-radius) var(--border-radius) 0;
}
</style>
