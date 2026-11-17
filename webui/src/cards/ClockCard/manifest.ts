import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { ClockCardConfig } from "@/cards/ClockCard/config.ts";

const plugin: CardPlugin<ClockCardConfig> = {
  name: "ClockCard",
  label: "文字时钟",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/ClockCard/ClockCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/ClockCard/ClockCardEditor.vue")),
  minWidth: 3,
  minHeight: 2,
  defaultConfig: {
    variant: "pixel",
    showSeconds: false,
    showDate: true,
    hour24: true,
    accentColor: "",
  },
};

export default plugin;
