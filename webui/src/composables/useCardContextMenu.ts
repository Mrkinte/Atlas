import { computed, ref } from "vue";
import type { DropdownOption } from "naive-ui";
import Delete24Regular from "@vicons/fluent/es/Delete24Regular.js";
import PersonLock24Regular from "@vicons/fluent/es/PersonLock24Regular.js";
import PhoneDesktop24Regular from "@vicons/fluent/es/PhoneDesktop24Regular.js";
import TextBulletListSquareEdit24Regular from "@vicons/fluent/es/TextBulletListSquareEdit24Regular.js";
import { getCardPlugin } from "@/cards/registry.ts";
import type { CardRequired, PlatformCardRequired } from "@/cards/types.ts";
import { iconConverter } from "@/utils/iconConverter.ts";

export function useCardContextMenu() {
  const x = ref(0);
  const y = ref(0);
  const show = ref(false);
  const activatedCard = ref<CardRequired>();

  const options = computed<DropdownOption[]>(() => {
    const editor = activatedCard.value
      ? getCardPlugin(activatedCard.value.type)?.editor
      : undefined;
    return [
      {
        label: "修改参数",
        key: "edit",
        icon: iconConverter(TextBulletListSquareEdit24Regular),
        disabled: !editor,
      },
      {
        label: "仅登录可见",
        key: "login-only",
        icon: iconConverter(PersonLock24Regular),
      },
      {
        label: "移动端隐藏",
        key: "mobile-hidden",
        icon: iconConverter(PhoneDesktop24Regular),
      },
      { label: "删除卡片", key: "delete", icon: iconConverter(Delete24Regular) },
    ];
  });

  function open(event: MouseEvent, item: PlatformCardRequired, cards: CardRequired[]) {
    x.value = event.clientX;
    y.value = event.clientY;
    activatedCard.value = cards.find((it) => it.id === item.i);
    show.value = true;
  }

  function close() {
    show.value = false;
  }

  return { x, y, show, options, activatedCard, open, close };
}
