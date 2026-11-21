import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { DockerCardConfig } from "@/cards/DockerCard/config.ts";

const plugin: CardPlugin<DockerCardConfig> = {
  name: "DockerCard",
  label: "Docker",
  display: defineAsyncComponent(() => import("@/cards/DockerCard/DockerCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/DockerCard/DockerCardEditor.vue")),
  minWidth: 4,
  minHeight: 3,
  previewSpan: 2,
  defaultConfig: {
    title: "Docker",
  },
};

export default plugin;
