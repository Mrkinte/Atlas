<script lang="ts" setup>
import { computed, nextTick, ref } from "vue";
import type { DropdownOption } from "naive-ui";
import MoreHorizontal24Filled from "@vicons/fluent/es/MoreHorizontal24Filled.js";
import BoxArrowLeft24Regular from "@vicons/fluent/es/BoxArrowLeft24Regular.js";
import Phone24Regular from "@vicons/fluent/es/Phone24Regular.js";
import Desktop24Regular from "@vicons/fluent/es/Desktop24Regular.js";
import AddSquare24Regular from "@vicons/fluent/es/AddSquare24Regular.js";
import Edit24Regular from "@vicons/fluent/es/Edit24Regular.js";
import Checkmark24Regular from "@vicons/fluent/es/Checkmark24Regular.js";
import { iconConverter } from "@/utils/iconConverter.ts";
import { demoMode } from "@/stores/demo.ts";
import { settings } from "@/stores/settings.ts";

const props = defineProps<{ editing: boolean }>();

const emit = defineEmits<{
  select: [key: string];
}>();

/** 选中选项时不关闭，仅在菜单外点击时关闭 */
const show = ref(false);
let reopenMenuOnSelect = false;

function handleShowChange(value: boolean) {
  if (value) {
    show.value = true;
    return;
  }
  // Naive UI 选中选项后会主动关闭菜单，此处拦截并在下一帧重新打开
  if (reopenMenuOnSelect) {
    reopenMenuOnSelect = false;
    void nextTick(() => {
      show.value = true;
    });
    return;
  }
  show.value = false;
}

const options = computed<Array<DropdownOption | { type: "divider"; key: string }>>(() => [
  // 演示站点由服务端自动登录，登出入口没有意义
  ...(demoMode.value
    ? []
    : [
        {
          label: "退出登录",
          key: "logout",
          icon: iconConverter(BoxArrowLeft24Regular),
        },
        { type: "divider" as const, key: "d1" },
      ]),
  // 平台切换只在编辑模式下可用
  ...(props.editing
    ? [
        {
          label: "编辑移动端",
          key: "mobile",
          icon: iconConverter(Phone24Regular),
        },
        {
          label: "编辑桌面端",
          key: "desktop",
          icon: iconConverter(Desktop24Regular),
        },
        { type: "divider" as const, key: "d2" },
        { label: "添加卡片", key: "add", icon: iconConverter(AddSquare24Regular) },
      ]
    : []),
  {
    label: props.editing ? "退出编辑" : "编辑布局",
    key: "toggle-edit",
    icon: iconConverter(props.editing ? Checkmark24Regular : Edit24Regular),
  },
]);

function handleSelect(key: string) {
  // 选中后保持菜单打开，重开逻辑见 handleShowChange
  reopenMenuOnSelect = true;
  emit("select", key);
}
</script>

<template>
  <n-dropdown
    :options="options"
    :show="show"
    placement="top-end"
    trigger="click"
    @select="handleSelect"
    @update:show="handleShowChange"
  >
    <n-button
      :style="{ bottom: settings.footerText.length ? 0 : '24px' }"
      circle
      class="more-button"
    >
      <template #icon>
        <n-icon>
          <MoreHorizontal24Filled />
        </n-icon>
      </template>
    </n-button>
  </n-dropdown>
</template>

<style scoped>
.more-button {
  position: absolute;
  right: 24px;
  z-index: 10;
}
</style>
