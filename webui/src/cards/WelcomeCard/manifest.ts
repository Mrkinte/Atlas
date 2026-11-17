import { defineAsyncComponent } from "vue";
import type { CardPlugin } from "@/cards/types";
import type { WelcomeCardConfig } from "@/cards/WelcomeCard/config.ts";

const plugin: CardPlugin<WelcomeCardConfig> = {
  name: "WelcomeCard",
  label: "欢迎卡片",
  // 用异步组件，避免首屏加载所有卡片代码
  display: defineAsyncComponent(() => import("@/cards/WelcomeCard/WelcomeCard.vue")),
  editor: defineAsyncComponent(() => import("@/cards/WelcomeCard/WelcomeCardEditor.vue")),
  minWidth: 2,
  minHeight: 1,
  defaultConfig: {
    title: "你好，欢迎回来！",
    subtitle: "这是一个副标题。",
    titleColor: "",
    subtitleColor: "",
    align: "left",
  },
};

export default plugin;
