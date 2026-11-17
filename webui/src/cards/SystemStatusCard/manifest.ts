import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { SystemStatusCardConfig } from "@/cards/SystemStatusCard/config.ts";

const plugin: CardPlugin<SystemStatusCardConfig> = {
  name: "SystemStatusCard",
  label: "系统状态",
  display: defineAsyncComponent(() => import("@/cards/SystemStatusCard/SystemStatusCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/SystemStatusCard/SystemStatusCardEditor.vue")),
  minWidth: 3,
  minHeight: 2,
  previewSpan: 2,
  defaultConfig: {
    title: "系统状态",
    variant: "classic",
    showMemory: true,
    showDisk: true,
    showLoad: true,
    showProcess: true,
    showUptime: true,
  },
};

export default plugin;
