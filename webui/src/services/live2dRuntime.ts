/**
 * 舞台运行时共享类型：库未公开的内部结构（pixi 应用、模型管理器等）集中在此声明
 */

import type { Oml2dEvents, Oml2dMethods, Oml2dProperties, Options } from "oh-my-live2d";
import type { Live2dHitTestPixiApp } from "@/composables/useLive2dHitTest.ts";

/** Cubism 强制播放优先级，用于打断当前动作 */
export const MOTION_PRIORITY_FORCE = 3;

/** 语音开关关闭时设为 0 */
export const DEFAULT_MODEL_VOLUME = 0.5;

export interface PixiAppLike {
  resize: () => void;
  /** 卸载时销毁以停止渲染循环 */
  app?: Live2dHitTestPixiApp & { destroy: (removeView?: boolean) => void };
}

export interface Live2dExpression {
  Name: string;
  File: string;
}

export interface Live2dMotion {
  File: string;
}

export interface Live2dMotionItem {
  group: string;
  index: number;
  name: string;
  file: string;
  /** 判断动作是否已归入自定义分组 */
  id?: string;
}

export interface Live2dMotionSection {
  key: string;
  group: string;
  items: Live2dMotionItem[];
}

/** 展示名可来自清单，运行名用于调用模型 */
export interface Live2dExpressionItem {
  runtimeName: string;
  name: string;
}

/** 表情与动作只能从库内部的 pixi-live2d-display 模型调用 */
export interface Live2dInternalModelLike {
  settings?: {
    expressions?: Live2dExpression[];
    motions?: Record<string, Live2dMotion[]>;
    /** 调试点击命中时映射可读名称 */
    hitAreas?: Array<{ Id?: string; Name?: string }>;
  };
  /** pixi-live2d-display 的 InternalModel 继承自 EventEmitter */
  on?: (event: string, handler: () => void) => void;
  off?: (event: string, handler: () => void) => void;
  /** 避免待机循环打断正在播放的动作 */
  motionManager?: {
    state?: {
      currentPriority?: number;
      reservePriority?: number;
    };
  };
  /** 在 beforeModelUpdate 中覆盖，只影响当前帧，下一帧自动恢复 */
  coreModel?: {
    setParameterValueById?: (id: string, value: number, weight?: number) => void;
    /** 调试点击命中用的只读接口 */
    getDrawableCount?: () => number;
    getDrawableIds?: () => string[];
    getDrawableVertexIndices?: (index: number) => Uint16Array | Uint32Array;
    getDrawableOpacity?: (index: number) => number;
    getDrawableDynamicFlagIsVisible?: (index: number) => boolean;
    /** 渲染顺序，数值越大越靠前 */
    getDrawableRenderOrders?: () => Int32Array;
  };
  update?: (deltaTime?: number, currentTime?: number) => unknown;
}

export interface Live2dModelLike {
  expression?: (id?: string) => Promise<boolean>;
  motion?: (group: string, index?: number, priority?: number) => Promise<boolean>;
  /** 参数为 HitAreas[].Name 数组 */
  on?: (event: string, handler: (names: string[]) => void) => void;
  off?: (event: string, handler: (names: string[]) => void) => void;
  /** 调试点击命中 ArtMesh 时使用 */
  toModelPosition?: (
    position: { x: number; y: number },
    result: { x: number; y: number },
  ) => { x: number; y: number };
  internalModel?: Live2dInternalModelLike;
}

export type Live2DInstance = Oml2dProperties &
  Oml2dMethods &
  Oml2dEvents & {
    /** 库未暴露 resize，缩放后需手动同步画布尺寸 */
    pixiApp?: PixiAppLike;
    /** 未出现在公开类型里，运行时可用 */
    models?: { model?: Live2dModelLike };
    /** 仅用于关掉库内置的入场问候 */
    tips?: { welcome?: () => void };
  };

/** 与 oml2d.tipsMessage 参数一致 */
export type Live2dTipsMessenger = (message: string, duration: number, priority: number) => void;

export function buildOml2dOptions(modelPath: string, scale: number, volume: number): Options {
  return {
    models: [{ path: modelPath, scale, volume }],
    // 站内右下角已有编辑入口，看板娘固定左下角，避免遮挡
    dockedPosition: "left",
    // 移动端不展示，避免小屏遮挡卡片
    mobileDisplay: false,
    sayHello: false,
    statusBar: { disable: true },
    // 菜单改用右键自绘，关闭库自带的悬浮菜单
    menus: { disable: true },
    // 复制文本时不弹出提示，减少对既有交互的干扰
    tips: {
      copyTips: { message: [] },
      // 毛玻璃气泡：去掉粗边框与投影，层级提到画布之上，移到舞台上方避免压住头部
      style: {
        zIndex: "9999",
        top: "auto",
        bottom: "100%",
        marginBottom: "10px",
        width: "min(76%, 300px)",
        minHeight: "0",
        padding: "10px 14px",
        fontSize: "14px",
        fontWeight: "500",
        lineHeight: "1.6",
        letterSpacing: "0.01em",
        color: "var(--atlas-tips-color)",
        backgroundColor: "var(--atlas-tips-bg)",
        border: "1px solid var(--atlas-tips-border)",
        borderRadius: "14px",
        boxShadow: "var(--atlas-tips-shadow)",
        filter: "none",
        backdropFilter: "blur(12px) saturate(1.15)",
        WebkitBackdropFilter: "blur(12px) saturate(1.15)",
        textAlign: "center",
      },
    },
  };
}
