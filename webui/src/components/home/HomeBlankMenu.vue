<script lang="ts" setup>
import { computed, ref } from "vue";
import type { DropdownOption } from "naive-ui";
import AddSquare24Regular from "@vicons/fluent/es/AddSquare24Regular.js";
import Phone24Regular from "@vicons/fluent/es/Phone24Regular.js";
import Desktop24Regular from "@vicons/fluent/es/Desktop24Regular.js";
import Edit24Regular from "@vicons/fluent/es/Edit24Regular.js";
import Checkmark24Regular from "@vicons/fluent/es/Checkmark24Regular.js";
import { iconConverter } from "@/utils/iconConverter.ts";

const props = defineProps<{ editing: boolean }>();

const emit = defineEmits<{
  select: [key: string];
}>();

const show = ref(false);
const x = ref(0);
const y = ref(0);

const options = computed<DropdownOption[]>(() => [
  ...(props.editing
    ? [
        {
          label: "添加卡片",
          key: "add",
          icon: iconConverter(AddSquare24Regular),
        },
        { type: "divider" as const, key: "d1" },
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
      ]
    : []),
  {
    label: props.editing ? "退出编辑" : "编辑布局",
    key: "toggle-edit",
    icon: props.editing ? iconConverter(Checkmark24Regular) : iconConverter(Edit24Regular),
  },
]);

function open(event: MouseEvent) {
  x.value = event.clientX;
  y.value = event.clientY;
  show.value = true;
}

function close() {
  show.value = false;
}

/** 右键用于在新位置重新打开菜单，不能当作“点击外部”关闭 */
function handleClickOutside(event: MouseEvent) {
  if (event.button === 2) return;
  show.value = false;
}

async function handleSelect(key: string) {
  if (key === "toggle-edit" && !props.editing) {
    // 进入编辑模式时保持菜单打开，展示切换后的选项
    emit("select", key);
  } else {
    show.value = false;
    emit("select", key);
  }
}

defineExpose({ open, close });
</script>

<template>
  <n-dropdown
    :on-clickoutside="handleClickOutside"
    :on-select="handleSelect"
    :options="options"
    :show="show"
    :x="x"
    :y="y"
    placement="bottom-start"
    trigger="manual"
  />
</template>
