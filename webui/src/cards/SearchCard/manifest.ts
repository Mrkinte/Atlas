import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";

const plugin: CardPlugin = {
  name: "SearchCard",
  label: "搜索卡片",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/SearchCard/SearchCard.vue")),
  editor: undefined,
  minWidth: 4,
  minHeight: 1,
  previewSpan: 2,
  defaultConfig: {},
};

export default plugin;
