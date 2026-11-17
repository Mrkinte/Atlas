import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { TypingCardConfig } from "@/cards/TypingCard/config.ts";

const plugin: CardPlugin<TypingCardConfig> = {
  name: "TypingCard",
  label: "打字机文字",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/TypingCard/TypingCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/TypingCard/TypingCardEditor.vue")),
  minWidth: 3,
  minHeight: 2,
  previewSpan: 2,
  defaultConfig: {
    text: "Hello, World!",
    color: "#4ade80",
    fontSize: 32,
    fontFamily: "electrolize",
    typingSpeed: 100,
    loop: false,
    loopInterval: 2000,
  },
};

export default plugin;
