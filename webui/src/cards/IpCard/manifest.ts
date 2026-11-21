import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { IpCardConfig } from "@/cards/IpCard/config.ts";

const plugin: CardPlugin<IpCardConfig> = {
  name: "IpCard",
  label: "IP 检测",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/IpCard/IpCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/IpCard/IpCardEditor.vue")),
  minWidth: 3,
  minHeight: 3,
  previewSpan: 2,
  defaultConfig: {
    showLocalIp: true,
    showIsp: true,
    showLocation: true,
    showLatency: true,
  },
};

export default plugin;
