import type { Live2dModelLike, Live2dMotion, Live2dMotionItem } from "@/services/live2dRuntime.ts";
import { MOTION_PRIORITY_FORCE } from "@/services/live2dRuntime.ts";

/** 动作结束后进入待机循环前的停顿，单位毫秒 */
const IDLE_LOOP_DELAY = 2000;

interface Live2dIdleLoopOptions {
  getModel: () => Live2dModelLike | undefined;
  isReady: () => boolean;
  /** 卸载后不再排程定时器 */
  isDestroyed: () => boolean;
  getIdleMotionId: () => string;
  /** 按配置标识解析动作：清单 id 优先，其次按动作文件查找 */
  resolveMotion: (identifier: string) => Live2dMotionItem | undefined;
  /** 以强制优先级播放动作，覆盖库自动播放的随机 idle */
  playMotion: (item: Live2dMotionItem) => void;
  /** 原始动作组，点击区域按组名匹配 */
  getMotions: () => Record<string, Live2dMotion[]>;
  closeMenu: () => void;
}

/**
 * 待机循环与可见性管理
 * 动作结束后延时进入配置的待机动作并循环；页面隐藏时暂停，回到前台恢复
 * 同时接入点击区域打断
 */
export function useLive2dIdleLoop(options: Live2dIdleLoopOptions) {
  /** 用户操作或切换模型时需取消 */
  let idleTimer: number | undefined;

  const hitInterruptModels = new WeakSet<Live2dModelLike>();

  let visibilityTracking = false;

  function clearIdleTimer() {
    if (idleTimer === undefined) return;

    window.clearTimeout(idleTimer);
    idleTimer = undefined;
  }

  /** 在动作结束后排程待机动作；播放期间若仍有高优先级动作则继续等待 */
  function scheduleIdle() {
    clearIdleTimer();
    if (options.isDestroyed() || document.hidden) return;
    if (!options.isReady() || !options.getIdleMotionId()) return;

    idleTimer = window.setTimeout(() => {
      idleTimer = undefined;
      if (!options.isReady() || options.isDestroyed() || document.hidden) return;

      const state = options.getModel()?.internalModel?.motionManager?.state;
      const priority = Math.max(state?.currentPriority ?? 0, state?.reservePriority ?? 0);

      // 普通/强制动作仍在播放时不打断，过一段时间再检查
      if (priority > 1) {
        scheduleIdle();
        return;
      }

      const item = options.resolveMotion(options.getIdleMotionId());
      if (!item) return;

      // 用强制优先级覆盖库自动播放的随机 idle，保证播放的是配置的待机动作
      options.playMotion(item);

      scheduleIdle();
    }, IDLE_LOOP_DELAY);
  }

  /** 页面切到后台时停止待机循环，回到前台后再恢复，避免后台持续占用渲染资源 */
  function handleVisibilityChange() {
    if (document.hidden) {
      clearIdleTimer();
      return;
    }

    if (options.isReady()) scheduleIdle();
  }

  function attachVisibility() {
    if (visibilityTracking) return;

    visibilityTracking = true;
    document.addEventListener("visibilitychange", handleVisibilityChange);
  }

  /**
   * HitAreas[].Name 绑定动作组，用强制优先级播放，
   * 打断右键动作、开机动作或循环中的待机动作
   */
  function enableHitInterrupt() {
    const model = options.getModel();
    if (!model?.on || hitInterruptModels.has(model)) return;

    const handler = (names: string[]) => {
      if (!options.isReady() || !Array.isArray(names)) return;

      clearIdleTimer();
      options.closeMenu();

      const groups = Object.keys(options.getMotions());
      const hitNames = names.map((name) => name.toLowerCase()).filter(Boolean);

      // HitAreas[].Name 通常就是动作组名（如 touch_idle7），优先精确匹配；
      // 否则取最长的包含匹配，避免 touch_idle7 被排在前面的 idle 抢先命中
      const exact = groups.find((name) => hitNames.includes(name.toLowerCase()));
      const group =
        exact ??
        groups
          .filter((name) => {
            const lower = name.toLowerCase();
            return hitNames.some((hit) => hit.includes(lower) || lower.includes(hit));
          })
          .sort((a, b) => b.length - a.length)[0];
      if (!group) return;

      const motions = options.getMotions()[group];
      const index = Math.floor(Math.random() * motions.length);
      void options.getModel()?.motion?.(group, index, MOTION_PRIORITY_FORCE);
      scheduleIdle();
    };

    model.on("hit", handler);
    hitInterruptModels.add(model);
  }

  function dispose() {
    if (visibilityTracking) {
      visibilityTracking = false;
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    }
    clearIdleTimer();
  }

  return {
    clearIdleTimer,
    scheduleIdle,
    enableHitInterrupt,
    attachVisibility,
    dispose,
  };
}
