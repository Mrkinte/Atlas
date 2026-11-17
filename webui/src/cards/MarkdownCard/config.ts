/** 编辑框与展示共用的最大字符数 */
export const MARKDOWN_CONTENT_MAX_LENGTH = 20000;

export const MARKDOWN_CONTENT_MAX_WIDTH = 1200;

export type MarkdownCardAlign = "left" | "center";

export type MarkdownCardConfig = {
  text: string;
  align: MarkdownCardAlign;
  /** 0 表示不限制 */
  maxContentWidth: number;
  /** 滚动到卡片边界后继续滚动首页 */
  bubbleScroll: boolean;
};
