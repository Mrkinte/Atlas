<script lang="ts" setup>
import { onMounted, ref } from "vue";

const dismissed = ref(false);

onMounted(() => {
  setTimeout(() => (dismissed.value = true), 3000);
});
</script>

<template>
  <div class="edit-tip-layer">
    <Transition mode="out-in" name="edit-tip-swap">
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
          <n-text>1. 拖动卡片边缘即可调整大小</n-text>
          <n-text>2. 按住 Ctrl 拖动卡片即可复制</n-text>
          <n-text>3. 右键卡片或空白处打开更多操作</n-text>
          <n-text>4. 选中卡片后，按下键盘 Delete 即可快速删除</n-text>
        </n-flex>
      </n-alert>

      <div v-else class="edit-tip-handle">
        <n-tooltip placement="right">
          <template #trigger>
            <n-button
              class="edit-tip-handle__button"
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
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.edit-tip-layer {
  position: absolute;
  top: 0;
  left: 0;
  z-index: 10;
  width: 480px;
}

.edit-tip {
  position: absolute;
  top: 24px;
  left: 24px;
}

.edit-tip-handle {
  position: absolute;
  top: 24px;
  left: 0;
}

.edit-tip-handle__button {
  height: auto;
  padding: 12px 5px;
  letter-spacing: 2px;
  writing-mode: vertical-rl;
  border-radius: 0 var(--border-radius) var(--border-radius) 0;
}

.edit-tip-swap-enter-active,
.edit-tip-swap-leave-active {
  transition:
    opacity 0.18s ease,
    transform 0.18s ease;
}

.edit-tip-swap-enter-from,
.edit-tip-swap-leave-to {
  opacity: 0;
  transform: translateX(-8px);
}
</style>
