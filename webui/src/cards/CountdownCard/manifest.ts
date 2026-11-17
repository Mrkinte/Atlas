import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { CountdownCardConfig } from "@/cards/CountdownCard/config.ts";

/** 默认示例：每年元旦，任何时间打开都有有效倒计时 */
const currentYear = new Date().getFullYear();

const plugin: CardPlugin<CountdownCardConfig> = {
  name: "CountdownCard",
  label: "倒计时",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/CountdownCard/CountdownCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/CountdownCard/CountdownCardEditor.vue")),
  minWidth: 2,
  minHeight: 2,
  defaultConfig: {
    title: "元旦",
    date: `${currentYear}-01-01`,
    repeatYearly: true,
    backgroundColor: "",
    backgroundImage: "",
  },
};

export default plugin;
