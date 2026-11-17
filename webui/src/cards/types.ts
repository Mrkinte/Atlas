import type { Component } from "vue";
import { type LayoutItem } from "grid-layout-plus";

export interface LayoutRequired {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface CardRequired {
  id: string;
  sm: LayoutRequired;
  lg: LayoutRequired;
  minWidth: number;
  minHeight: number;
  /** 是否匿名可见；false 表示仅登录可见 */
  isVisible: boolean;
  /** 移动端是否显示 */
  mobileVisible: boolean;
  /** 卡片类型名，对应 plugin.name */
  type: string;
  config: Record<string, unknown>;
}

export interface PlatformCardRequired extends LayoutItem {
  /** 是否匿名可见；false 表示仅登录可见 */
  isVisible: boolean;
  /** 移动端是否显示 */
  mobileVisible: boolean;
  /** 卡片类型名，对应 plugin.name */
  type: string;
  config: Record<string, unknown>;
}

export interface CardSaveContext {
  data: Record<string, unknown>;
}

export interface CardPlugin<TData = Record<string, unknown>> {
  /** 唯一标识，如 'WelcomeCard' */
  name: string;
  label?: string;
  display: Component;
  editor?: Component;
  minWidth: number;
  minHeight: number;
  /** 预览占用的列数，2 表示占满一行 */
  previewSpan?: number;
  defaultConfig: TData;
  /** 首页可达性探测目标，返回 null 表示不参与探测；由页面级调度器发起 */
  probeTarget?: (config: Record<string, unknown>) => string | null;
  onSave?: (ctx: CardSaveContext) => void | Promise<void>;
  version?: string;
  author?: string;
}

export interface CardEditorExpose {
  /** 返回 false 时由编辑器自行展示错误提示 */
  validate: () => Promise<boolean>;
  getConfig: () => Record<string, unknown>;
  reset?: () => void;
}
