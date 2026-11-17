import { ref } from "vue";
import { isAuthenticated } from "@/stores/auth.ts";
import type { Live2dLayout } from "@/services/live2dModelConfig.ts";

/** oh-my-live2d 创建的舞台容器 ID */
const STAGE_ELEMENT_ID = "oml2d-stage";

/** 边缘吸附偏好的本地存储键：属于浏览器交互偏好，不写入服务端配置 */
const EDGE_SNAP_STORAGE_KEY = "live2d-edge-snap";

type LayoutPatch = Partial<Live2dLayout>;

interface Live2dLayoutOptions {
  /** 保存布局时一并写入 */
  getScale: () => number;
  isReady: () => boolean;
  readLayout: () => Live2dLayout;
  applyLayout: (patch: LayoutPatch) => void;
  saveLayout: () => Promise<string | undefined>;
}

/**
 * 布局持久化：边缘吸附、位置还原与拖动/缩放后的保存队列
 * 未登录时只更新本地状态，不发必然 401 的请求
 */
export function useLive2dLayout(options: Live2dLayoutOptions) {
  /** 默认开启；只有浏览器明确记录为关闭时才允许移出视口 */
  const edgeSnap = ref(localStorage.getItem(EDGE_SNAP_STORAGE_KEY) !== "0");

  /** 用户正在拖动或缩放，期间不要用保存的比例覆盖位置 */
  let adjusting = false;

  let layoutSaveTimer: number | undefined;
  let layoutSavePending = false;
  let layoutSaveRunning = false;

  /** 连续几帧的定位任务，销毁舞台时需要取消 */
  let positionFrame = 0;
  let stageObserver: ResizeObserver | undefined;

  function beginAdjusting() {
    adjusting = true;
  }

  function endAdjusting() {
    adjusting = false;
  }

  function isAdjusting() {
    return adjusting;
  }

  function setEdgeSnap(enabled: boolean) {
    edgeSnap.value = enabled;
    localStorage.setItem(EDGE_SNAP_STORAGE_KEY, enabled ? "1" : "0");
  }

  /**
   * 开启吸附时按可移动范围的比例换算；
   * 关闭时按视口宽高换算，窗口尺寸变化后保持相对位置
   */
  function applyStoredPosition() {
    const stage = document.getElementById(STAGE_ELEMENT_ID);
    if (!stage) return;

    const rect = stage.getBoundingClientRect();
    const maxLeft = Math.max(0, window.innerWidth - rect.width);
    const maxTop = Math.max(0, window.innerHeight - rect.height);

    const layout = options.readLayout();
    // 尚未记录自由位置时回退到吸附比例
    const freeX = edgeSnap.value ? null : layout.freeRatioX;
    const freeY = edgeSnap.value ? null : layout.freeRatioY;

    stage.style.left = `${
      freeX !== null ? freeX * window.innerWidth : layout.positionX * maxLeft
    }px`;
    stage.style.top = `${
      freeY !== null ? freeY * window.innerHeight : layout.positionY * maxTop
    }px`;
    stage.style.bottom = "auto";
    stage.style.right = "auto";
  }

  /**
   * 渲染库加载完成后会连续几帧调整舞台尺寸，
   * 连算几帧保证最终位置落在保存的比例上
   */
  function settleStoredPosition(frames = 4) {
    cancelAnimationFrame(positionFrame);
    const step = (remaining: number) => {
      applyStoredPosition();
      if (remaining > 1) {
        positionFrame = requestAnimationFrame(() => step(remaining - 1));
      }
    };
    step(frames);
  }

  /** 合并短时间内的多次拖动/缩放，只发送最后一次布局 */
  function scheduleLayoutSave(patch: LayoutPatch) {
    // 先更新本地模型配置：未登录时布局只在本机生效，不发必然 401 的请求
    options.applyLayout(patch);
    if (!isAuthenticated.value) return;

    layoutSavePending = true;
    if (layoutSaveTimer !== undefined) window.clearTimeout(layoutSaveTimer);

    layoutSaveTimer = window.setTimeout(() => {
      layoutSaveTimer = undefined;
      void flushLayoutSave();
    }, 250);
  }

  /** 串行发送，避免旧响应把布局回写成旧值 */
  async function flushLayoutSave() {
    if (layoutSaveRunning) return;

    layoutSaveRunning = true;
    try {
      while (layoutSavePending) {
        layoutSavePending = false;

        // 会话在防抖期间失效时不再请求，布局已落到本地状态
        if (!isAuthenticated.value) return;

        if (await options.saveLayout()) return;
      }
    } finally {
      layoutSaveRunning = false;
    }
  }

  function persistLayout() {
    const stage = document.getElementById(STAGE_ELEMENT_ID);
    if (!stage) return;

    const scale = options.getScale();
    const rect = stage.getBoundingClientRect();

    // 关闭边缘吸附时按视口宽高的比例保存，允许负值与超出 1，窗口尺寸变化后可等比还原
    if (!edgeSnap.value) {
      scheduleLayoutSave({
        scale,
        freeRatioX: rect.left / window.innerWidth,
        freeRatioY: rect.top / window.innerHeight,
      });
      return;
    }

    const maxLeft = Math.max(0, window.innerWidth - rect.width);
    const maxTop = Math.max(0, window.innerHeight - rect.height);

    scheduleLayoutSave({
      scale,
      positionX: maxLeft > 0 ? Math.min(1, Math.max(0, rect.left / maxLeft)) : 0,
      positionY: maxTop > 0 ? Math.min(1, Math.max(0, rect.top / maxTop)) : 0,
    });
  }

  function clampStageIntoViewport() {
    const stage = document.getElementById(STAGE_ELEMENT_ID);
    if (!stage) return;

    const rect = stage.getBoundingClientRect();
    const maxLeft = Math.max(0, window.innerWidth - rect.width);
    const maxTop = Math.max(0, window.innerHeight - rect.height);
    const left = Math.min(maxLeft, Math.max(0, rect.left));
    const top = Math.min(maxTop, Math.max(0, rect.top));
    if (left === rect.left && top === rect.top) return;

    stage.style.left = `${left}px`;
    stage.style.top = `${top}px`;
    stage.style.bottom = "auto";
    stage.style.right = "auto";
    persistLayout();
  }

  /**
   * 监听舞台尺寸变化并按保存的比例重新定位
   * 渲染库加载完成后才缩放到目标尺寸，尺寸确定后才能算出可移动范围
   */
  function observeStageSize() {
    const stage = document.getElementById(STAGE_ELEMENT_ID);
    if (!stage) return;

    stageObserver?.disconnect();
    stageObserver = new ResizeObserver(() => {
      if (!options.isReady() || adjusting) return;
      applyStoredPosition();
    });
    stageObserver.observe(stage);
  }

  /** 视口变化时可移动范围也变，需按比例重算 */
  function handleViewportResize() {
    if (!options.isReady() || adjusting) return;
    applyStoredPosition();
  }

  function startTracking() {
    observeStageSize();
    window.addEventListener("resize", handleViewportResize);
  }

  /** 停止跟踪并写回最后位置 */
  function dispose() {
    if (layoutSaveTimer !== undefined) {
      window.clearTimeout(layoutSaveTimer);
      layoutSaveTimer = undefined;
    }
    void flushLayoutSave();

    stageObserver?.disconnect();
    stageObserver = undefined;
    cancelAnimationFrame(positionFrame);
    window.removeEventListener("resize", handleViewportResize);
  }

  return {
    edgeSnap,
    setEdgeSnap,
    beginAdjusting,
    endAdjusting,
    isAdjusting,
    applyStoredPosition,
    settleStoredPosition,
    persistLayout,
    clampStageIntoViewport,
    startTracking,
    dispose,
  };
}
