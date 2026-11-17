<script lang="ts" setup>
import { computed, type CSSProperties } from "vue";
import { useThemeVars } from "naive-ui";
import type { MarkdownCardConfig } from "@/cards/MarkdownCard/config.ts";
import { renderMarkdown } from "@/cards/MarkdownCard/markdown.ts";

/** 历史数据可能缺字段，全部可选并由默认值补齐 */
const props = withDefaults(defineProps<Partial<MarkdownCardConfig>>(), {
  text: "",
  align: "left",
  maxContentWidth: 0,
  bubbleScroll: true,
});

const themeVars = useThemeVars();

/** 解析层已禁用原始 HTML，输出只含 Markdown 生成的标签，可安全用于 v-html */
const html = computed(() => renderMarkdown(props.text));

/** 把主题色注入 CSS 变量，让排版与代码高亮跟随明暗主题 */
const themeStyle = computed<CSSProperties>(() => ({
  "--markdown-link": themeVars.value.primaryColor,
  "--markdown-code-bg": themeVars.value.codeColor,
  "--markdown-border": themeVars.value.borderColor,
  "--markdown-text-secondary": themeVars.value.textColor3,
  "--markdown-success": themeVars.value.successColor,
  "--markdown-warning": themeVars.value.warningColor,
  "--markdown-info": themeVars.value.infoColor,
}));

const bodyStyle = computed<CSSProperties>(() => ({
  textAlign: props.align,
  maxWidth: props.maxContentWidth > 0 ? `${props.maxContentWidth}px` : undefined,
}));
</script>

<template>
  <div
    :class="{ 'markdown-card--contain': !bubbleScroll }"
    :style="themeStyle"
    class="markdown-card"
  >
    <div :style="bodyStyle" class="markdown-card__body" v-html="html" />
  </div>
</template>

<style scoped>
/* JetBrains Mono（SIL OFL 1.1），仅含拉丁字形 */
@font-face {
  font-family: "JetBrains Mono";
  src: url("../../assets/fonts/jetbrains-mono-regular.woff2") format("woff2");
  font-display: swap;
  font-weight: 400;
}

@font-face {
  font-family: "JetBrains Mono";
  src: url("../../assets/fonts/jetbrains-mono-bold.woff2") format("woff2");
  font-display: swap;
  font-weight: 700;
}

.markdown-card {
  box-sizing: border-box;
  width: 100%;
  height: 100%;
  /* 卡片选择器预览处高度自适应，用最小高度兜底 */
  min-height: 64px;
  padding: 8px 10px;
  overflow: auto;
  line-height: 1.55;
  font-size: 14px;
  overflow-wrap: anywhere;
}

/* 关闭冒泡时隔离滚动，避免卡片内滚动带动首页 */
.markdown-card--contain {
  overscroll-behavior: contain;
}

.markdown-card::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}

.markdown-card::-webkit-scrollbar-thumb {
  border-radius: 3px;
  background-color: var(--markdown-border);
}

.markdown-card__body {
  box-sizing: border-box;
  width: 100%;
  margin: 0 auto;
}

.markdown-card :deep(h1),
.markdown-card :deep(h2),
.markdown-card :deep(h3),
.markdown-card :deep(h4),
.markdown-card :deep(h5),
.markdown-card :deep(h6) {
  margin: 0.6em 0 0.35em;
  font-weight: 600;
  line-height: 1.3;
}

.markdown-card :deep(h1) {
  font-size: 1.6em;
}

.markdown-card :deep(h2) {
  font-size: 1.35em;
}

.markdown-card :deep(h3) {
  font-size: 1.18em;
}

.markdown-card :deep(h4) {
  font-size: 1.05em;
}

.markdown-card :deep(h5) {
  font-size: 0.95em;
}

.markdown-card :deep(h6) {
  font-size: 0.88em;
  opacity: 0.8;
}

.markdown-card :deep(p) {
  margin: 0.4em 0;
}

.markdown-card :deep(ul),
.markdown-card :deep(ol) {
  margin: 0.35em 0;
  padding-left: 1.5em;
}

.markdown-card :deep(li) {
  margin: 0.15em 0;
}

.markdown-card :deep(li > p) {
  margin: 0.15em 0;
}

.markdown-card :deep(blockquote) {
  margin: 0.5em 0;
  padding: 0.1em 0.8em;
  border-left: 3px solid var(--markdown-border);
  color: var(--markdown-text-secondary);
}

.markdown-card :deep(hr) {
  margin: 0.8em 0;
  border: none;
  border-top: 1px solid var(--markdown-border);
}

.markdown-card :deep(a) {
  color: var(--markdown-link);
  text-decoration: none;
}

.markdown-card :deep(a:hover) {
  text-decoration: underline;
}

.markdown-card :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 6px;
}

.markdown-card :deep(code) {
  padding: 0.1em 0.35em;
  border-radius: 4px;
  background-color: var(--markdown-code-bg);
  font-family: "JetBrains Mono", ui-monospace, "SFMono-Regular", Consolas, monospace;
  font-size: 0.88em;
}

.markdown-card :deep(pre) {
  margin: 0.5em 0;
  padding: 8px 10px;
  overflow-x: auto;
  border-radius: 6px;
  background-color: var(--markdown-code-bg);
}

.markdown-card :deep(pre code) {
  display: block;
  padding: 0;
  background-color: transparent;
  font-size: 0.85em;
  line-height: 1.5;
}

/* 代码高亮 token 配色，复用主题色保证明暗主题都可读 */
.markdown-card :deep(.hljs-comment),
.markdown-card :deep(.hljs-quote) {
  color: var(--markdown-text-secondary);
  font-style: italic;
}

.markdown-card :deep(.hljs-keyword),
.markdown-card :deep(.hljs-selector-tag),
.markdown-card :deep(.hljs-literal),
.markdown-card :deep(.hljs-built_in) {
  color: var(--markdown-link);
}

.markdown-card :deep(.hljs-string),
.markdown-card :deep(.hljs-attr),
.markdown-card :deep(.hljs-template-variable) {
  color: var(--markdown-success);
}

.markdown-card :deep(.hljs-number),
.markdown-card :deep(.hljs-symbol),
.markdown-card :deep(.hljs-bullet) {
  color: var(--markdown-warning);
}

.markdown-card :deep(.hljs-title),
.markdown-card :deep(.hljs-section),
.markdown-card :deep(.hljs-name) {
  color: var(--markdown-info);
}

.markdown-card :deep(.hljs-emphasis) {
  font-style: italic;
}

.markdown-card :deep(.hljs-strong) {
  font-weight: 600;
}

.markdown-card :deep(table) {
  display: block;
  max-width: 100%;
  margin: 0.5em 0;
  overflow-x: auto;
  border-collapse: collapse;
}

.markdown-card :deep(th),
.markdown-card :deep(td) {
  padding: 4px 10px;
  border: 1px solid var(--markdown-border);
  text-align: left;
}

.markdown-card :deep(th) {
  background-color: var(--markdown-code-bg);
  font-weight: 600;
}

.markdown-card :deep(.task-list-item) {
  margin-left: -1.2em;
  list-style: none;
}

.markdown-card :deep(.task-list-item-checkbox) {
  margin-right: 0.4em;
  vertical-align: middle;
}
</style>
