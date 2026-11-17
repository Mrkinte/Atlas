import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { MarkdownCardConfig } from "@/cards/MarkdownCard/config.ts";

const plugin: CardPlugin<MarkdownCardConfig> = {
  name: "MarkdownCard",
  label: "Markdown 卡片",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/MarkdownCard/MarkdownCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/MarkdownCard/MarkdownCardEditor.vue")),
  minWidth: 3,
  minHeight: 2,
  previewSpan: 2,
  defaultConfig: {
    text: "# 你好\n\n这是一个 **Markdown** 卡片。\n\n- 支持列表与 `行内代码`\n- 支持 [链接](https://example.com)\n",
    align: "left",
    maxContentWidth: 0,
    bubbleScroll: true,
  },
};

export default plugin;
