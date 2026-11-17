<script lang="ts" setup>
import { h } from "vue";
import type { DropdownOption } from "naive-ui";
import { useCardContextMenu } from "@/composables/useCardContextMenu.ts";
import type { CardRequired } from "@/cards/types.ts";

const emit = defineEmits<{
  select: [key: string, card: CardRequired];
}>();

const { x, y, show, options, activatedCard, open, close } = useCardContextMenu();

function getToggleStatus(key: string): string | null {
  if (key === "login-only") return activatedCard.value?.isVisible === false ? "已开启" : "已关闭";
  if (key === "mobile-hidden")
    return activatedCard.value?.mobileVisible === false ? "已开启" : "已关闭";
  return null;
}

function renderLabel(option: DropdownOption) {
  const status = getToggleStatus(String(option.key));
  if (!status) return option.label as string;
  return h(
    "div",
    {
      style: {
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        gap: "24px",
        minWidth: "160px",
      },
    },
    [option.label as string, h("span", { style: { opacity: "0.6", fontSize: "12px" } }, status)],
  );
}

function handleSelect(key: string) {
  close();
  const card = activatedCard.value;
  if (!card) return;
  emit("select", key, card);
}

/** 右键用于在新位置重新打开菜单，不能当作“点击外部”关闭 */
function handleClickOutside(event: MouseEvent) {
  if (event.button === 2) return;
  close();
}

defineExpose({ open, close });
</script>

<template>
  <n-dropdown
    :on-clickoutside="handleClickOutside"
    :on-select="handleSelect"
    :options="options"
    :render-label="renderLabel"
    :show="show"
    :x="x"
    :y="y"
    placement="bottom-start"
    trigger="manual"
  />
</template>
