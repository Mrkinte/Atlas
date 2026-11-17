import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { ServiceCardConfig } from "@/cards/ServiceCard/config.ts";
import { resolveProbeTarget } from "@/cards/ServiceCard/probe.ts";

const plugin: CardPlugin<ServiceCardConfig> = {
  name: "ServiceCard",
  label: "NAS应用",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/ServiceCard/ServiceCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/ServiceCard/ServiceCardEditor.vue")),
  minWidth: 1,
  minHeight: 1,
  // 页面级调度器调用，与点击卡片共用地址推断规则
  probeTarget: (config) => resolveProbeTarget(config as Partial<ServiceCardConfig>),
  defaultConfig: {
    name: "qBittorrent",
    description: "",
    hostMode: "auto",
    protocol: "auto",
    port: 8080,
    path: "",
    url1: "",
    url2: "",
    url3: "",
    icon: "",
    openMethod: "_blank",
  },
};

export default plugin;
