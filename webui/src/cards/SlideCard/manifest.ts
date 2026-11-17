import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { SlideCardConfig } from "@/cards/SlideCard/config.ts";

const plugin: CardPlugin<SlideCardConfig> = {
  name: "SlideCard",
  label: "图片轮播",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/SlideCard/SlideCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/SlideCard/SlideCardEditor.vue")),
  minWidth: 4,
  minHeight: 2,
  previewSpan: 2,
  defaultConfig: {
    slides: [],
    autoplay: true,
    interval: 5,
    objectFit: "cover",
    showTitle: true,
    showDots: true,
    showArrow: true,
    titleColor: "",
  },
};

export default plugin;
