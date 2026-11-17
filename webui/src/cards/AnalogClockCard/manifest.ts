import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { AnalogClockConfig } from "@/cards/AnalogClockCard/config.ts";

const plugin: CardPlugin<AnalogClockConfig> = {
  name: "AnalogClockCard",
  label: "表盘时钟",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/AnalogClockCard/AnalogClock.vue")),
  editor: defineAsyncComponent(() => import("@/cards/AnalogClockCard/AnalogClockEditor.vue")),
  minWidth: 2,
  minHeight: 2,
  defaultConfig: {
    showSeconds: true,
    smoothSeconds: false,
    showNumbers: false,
    accentColor: "",
    backgroundOpacity: 100,
  },
};

export default plugin;
