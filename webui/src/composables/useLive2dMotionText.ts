import { onBeforeUnmount, watchEffect } from "vue";
import { useRoute } from "vue-router";
import { appTheme } from "@/stores/theme.ts";
import { registerLive2dFeedback } from "@/services/live2dFeedback.ts";
import type { Live2dManifest, Live2dManifestMotion } from "@/services/live2dManifest.ts";
import {
  type Live2DInstance,
  type Live2dModelLike,
  type Live2dMotion,
} from "@/services/live2dRuntime.ts";

/** oh-my-live2d 创建的舞台容器 ID */
const STAGE_ELEMENT_ID = "oml2d-stage";

/** 动作气泡的提示优先级，与复制提示同级，可以覆盖同级旧提示 */
const MOTION_TEXT_PRIORITY = 3;

/** 清单没有声明时长时的兜底值，单位毫秒 */
const MOTION_TEXT_FALLBACK_DURATION = 4000;

/** 气泡配色随站点明暗主题切换，通过根元素变量让库的内联样式实时更新 */
const TIPS_THEME_VARS = {
  light: {
    "--atlas-tips-bg": "rgba(255, 255, 255, 0.92)",
    "--atlas-tips-border": "rgba(0, 0, 0, 0.08)",
    "--atlas-tips-color": "rgba(20, 20, 24, 0.88)",
    "--atlas-tips-shadow": "0 10px 28px rgba(0, 0, 0, 0.14)",
  },
  dark: {
    "--atlas-tips-bg": "rgba(24, 24, 28, 0.86)",
    "--atlas-tips-border": "rgba(255, 255, 255, 0.14)",
    "--atlas-tips-color": "rgba(255, 255, 255, 0.92)",
    "--atlas-tips-shadow": "0 10px 28px rgba(0, 0, 0, 0.28)",
  },
} as const;

interface MotionTextEntry {
  text: string;
  duration?: number;
}

interface Live2dMotionTextOptions {
  /** 台词与提示都通过它的气泡展示 */
  getOml2d: () => Live2DInstance | undefined;
  isReady: () => boolean;
  /** 原始动作组，用于按组名与下标反查动作文件 */
  getMotions: () => Record<string, Live2dMotion[]>;
  /** 台词文案与默认时长都来自清单 */
  getManifest: () => Live2dManifest | undefined;
}

/**
 * 动作台词气泡与站点提示承接
 * 动作开始播放后按清单弹出台词；首页无弹层时站点轻提示也由气泡承接
 */
export function useLive2dMotionText(options: Live2dMotionTextOptions) {
  const route = useRoute();

  /** 已接入台词气泡的模型实例；重新加载后是新实例，需要重新接入 */
  const motionTextModels = new WeakSet<Live2dModelLike>();

  let disposeFeedback: (() => void) | undefined;

  /** 舞台真实可见时才承接操作提示，移动端隐藏或未挂载时回退 toast */
  function isStageVisible(): boolean {
    const stage = document.getElementById(STAGE_ELEMENT_ID);
    if (!stage) return false;

    const style = getComputedStyle(stage);
    const rect = stage.getBoundingClientRect();
    return (
      style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0
    );
  }

  /** 弹层会遮挡看板娘气泡；存在这些元素时把提示交回站点 toast */
  const OVERLAY_SELECTOR = ".n-modal, .n-dialog, .n-drawer, .n-image-preview-container";

  /** 只统计真正可见的弹层，避免关闭动画/常驻容器造成误判 */
  function hasBlockingOverlay(): boolean {
    return Array.from(document.querySelectorAll(OVERLAY_SELECTOR)).some((element) => {
      const rect = element.getBoundingClientRect();
      return rect.width > 0 && rect.height > 0;
    });
  }

  /**
   * 按运行时组名与下标找到清单动作：先查当前动作文件，
   * 再用「组名 + 文件」匹配，未导出的动作不会打乱其他动作的台词
   */
  function resolveManifestMotion(group: string, index?: number): Live2dManifestMotion | undefined {
    if (typeof index !== "number" || index < 0) return undefined;
    const file = options.getMotions()[group]?.[index]?.File;
    if (!file) return undefined;

    return options
      .getManifest()
      ?.motions.find((motion) => motion.file === file && (!motion.group || motion.group === group));
  }

  /** 清单未配置台词时保持静默 */
  function resolveMotionText(group: string, index?: number): MotionTextEntry | undefined {
    const motion = resolveManifestMotion(group, index);
    if (!motion?.text) return undefined;
    return {
      text: motion.text,
      duration: motion.duration ?? options.getManifest()?.defaultDuration,
    };
  }

  function showMotionText(group: string | undefined, index?: number) {
    if (!group || !options.isReady() || !isStageVisible()) return;

    const entry = resolveMotionText(group, index);
    if (!entry) return;

    options
      .getOml2d()
      ?.tipsMessage(
        entry.text,
        entry.duration ?? options.getManifest()?.defaultDuration ?? MOTION_TEXT_FALLBACK_DURATION,
        MOTION_TEXT_PRIORITY,
      );
  }

  /**
   * 接到模型的动作入口上：右键菜单、点击区域与恢复的默认动作最终都会调用
   * model.motion，包一层即可覆盖全部来源
   */
  function enableMotionTexts() {
    const model = options.getOml2d()?.models?.model;
    if (!model?.motion || motionTextModels.has(model)) return;

    const play = model.motion.bind(model);
    model.motion = async (group, index, priority) => {
      const started = await play(group, index, priority);
      if (!started) return false;

      showMotionText(group, index);
      return started;
    };
    motionTextModels.add(model);
  }

  function enableFeedback() {
    disposeFeedback = registerLive2dFeedback((message, level) => {
      // 只在首页且没有弹层时接管轻提示；设置页、登录页与弹窗里的结果继续用 toast
      if (route.name !== "HomeView" || hasBlockingOverlay()) return false;
      if (!options.isReady() || !isStageVisible()) return false;

      options.getOml2d()?.tipsMessage(message, level === "success" ? 2400 : 2600, 3);
      return true;
    });
  }

  // 主题切换时同步气泡配色变量；提示样式引用变量，无需重建舞台
  watchEffect(() => {
    const vars = appTheme.value.name === "dark" ? TIPS_THEME_VARS.dark : TIPS_THEME_VARS.light;
    const style = document.documentElement.style;
    for (const [key, value] of Object.entries(vars)) {
      style.setProperty(key, value);
    }
  });

  onBeforeUnmount(() => {
    for (const key of Object.keys(TIPS_THEME_VARS.light)) {
      document.documentElement.style.removeProperty(key);
    }
  });

  function dispose() {
    disposeFeedback?.();
    disposeFeedback = undefined;
  }

  return { enableMotionTexts, enableFeedback, dispose };
}
