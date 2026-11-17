import { createVNode, ref, render } from "vue";
import CaretDownRight24Regular from "@vicons/fluent/es/CaretDownRight24Regular.js";
import { LIVE2D_LAYOUT_DEFAULTS, LIVE2D_LAYOUT_LIMITS } from "@/config/default.ts";
import type { Live2DInstance } from "@/services/live2dRuntime.ts";
import type { useLive2dLayout } from "@/composables/useLive2dLayout.ts";

/** oh-my-live2d 创建的舞台容器 ID，拖动与缩放交互都创建在该元素上 */
const STAGE_ELEMENT_ID = "oml2d-stage";

/** 位移超过该阈值才视为拖动，避免点击看板娘时误移动 */
const DRAG_THRESHOLD = 4;

const MIN_MODEL_SCALE = LIVE2D_LAYOUT_LIMITS.scale.min;
const MAX_MODEL_SCALE = LIVE2D_LAYOUT_LIMITS.scale.max;

type Live2dLayoutApi = ReturnType<typeof useLive2dLayout>;

interface Live2dStageInteractionOptions {
  /** 缩放时同步模型比例与画布尺寸 */
  getOml2d: () => Live2DInstance | undefined;
  isReady: () => boolean;
  getScale: () => number;
  setScale: (scale: number) => void;
  /** 拖动/缩放期间暂停回写，结束后保存 */
  layout: Pick<Live2dLayoutApi, "edgeSnap" | "beginAdjusting" | "endAdjusting" | "persistLayout">;
}

/**
 * 舞台拖拽与缩放：
 * 整体跟随指针拖动（受边缘吸附约束），调整模式下用右下角手柄改变尺寸
 */
export function useLive2dStageInteraction(options: Live2dStageInteractionOptions) {
  const resizeMode = ref(false);

  /** 模型加载完成或调整状态切换时同步显示/隐藏 */
  let resizeHandle: HTMLElement | undefined;

  let disposeDrag: (() => void) | undefined;
  let disposeResize: (() => void) | undefined;

  function updateResizeHandleVisibility() {
    if (!resizeHandle) return;
    resizeHandle.style.display = resizeMode.value && options.isReady() ? "block" : "none";
  }

  function setResizeMode(enabled: boolean) {
    resizeMode.value = enabled;
    updateResizeHandleVisibility();

    // 用虚线标出舞台范围；outline 不占布局，不会改变尺寸
    const stage = document.getElementById(STAGE_ELEMENT_ID);
    if (!stage) return;
    stage.style.outline = enabled ? "1px dashed rgba(127, 127, 127, 0.8)" : "";
    stage.style.outlineOffset = enabled ? "-1px" : "";
  }

  function enableStageDrag() {
    const target = document.getElementById(STAGE_ELEMENT_ID);
    if (!target) return;

    // 显式标注非空类型，便于下方回调使用
    const stage: HTMLElement = target;

    stage.style.cursor = "grab";
    // 触摸拖动时禁止页面滚动手势
    stage.style.touchAction = "none";

    let pointerId = -1;
    let startX = 0;
    let startY = 0;
    let originLeft = 0;
    let originTop = 0;
    let stageWidth = 0;
    let stageHeight = 0;
    // 是否真正拖动过，避免单击时也触发保存
    let moved = false;

    function handlePointerDown(event: PointerEvent) {
      if (event.button !== 0 || pointerId !== -1) return;

      const rect = stage.getBoundingClientRect();
      // 舞台默认用 bottom 定位，拖动前先换算成 left/top，之后只更新这两个值
      stage.style.left = `${rect.left}px`;
      stage.style.top = `${rect.top}px`;
      stage.style.bottom = "auto";
      stage.style.right = "auto";

      pointerId = event.pointerId;
      startX = event.clientX;
      startY = event.clientY;
      originLeft = rect.left;
      originTop = rect.top;
      moved = false;
      options.layout.beginAdjusting();
      // 用实际渲染尺寸而非取整的 offsetWidth，避免边界处溢出视口
      stageWidth = rect.width;
      stageHeight = rect.height;
      stage.style.cursor = "grabbing";
      // 这里不能立刻捕获指针：捕获会把 pointerup 重定向到舞台，
      // pixi 收不到 canvas 上的 pointertap，模型的命中区点击就失效了，留到真正拖动时再捕获
    }

    function handlePointerMove(event: PointerEvent) {
      if (event.pointerId !== pointerId) return;

      const offsetX = event.clientX - startX;
      const offsetY = event.clientY - startY;
      if (Math.abs(offsetX) < DRAG_THRESHOLD && Math.abs(offsetY) < DRAG_THRESHOLD) {
        return;
      }

      moved = true;

      // 确认为拖动后才捕获指针，保证继续拖出舞台时仍能收到 pointermove
      if (!stage.hasPointerCapture(pointerId)) {
        stage.setPointerCapture(pointerId);
      }

      if (options.layout.edgeSnap.value) {
        // 开启边缘吸附时限制在视口内，保证拖出边界时停在边缘
        const maxLeft = Math.max(0, window.innerWidth - stageWidth);
        const maxTop = Math.max(0, window.innerHeight - stageHeight);

        stage.style.left = `${Math.min(maxLeft, Math.max(0, originLeft + offsetX))}px`;
        stage.style.top = `${Math.min(maxTop, Math.max(0, originTop + offsetY))}px`;
        return;
      }

      // 关闭边缘吸附后不再收敛位置，允许出现负坐标，移出视口的部分由浏览器裁掉
      stage.style.left = `${originLeft + offsetX}px`;
      stage.style.top = `${originTop + offsetY}px`;
    }

    function handlePointerEnd(event: PointerEvent) {
      if (event.pointerId !== pointerId) return;

      pointerId = -1;
      options.layout.endAdjusting();
      stage.style.cursor = "grab";
      if (stage.hasPointerCapture(event.pointerId)) {
        stage.releasePointerCapture(event.pointerId);
      }
      if (moved) options.layout.persistLayout();
    }

    stage.addEventListener("pointerdown", handlePointerDown);
    stage.addEventListener("pointermove", handlePointerMove);
    stage.addEventListener("pointerup", handlePointerEnd);
    stage.addEventListener("pointercancel", handlePointerEnd);

    disposeDrag = () => {
      stage.removeEventListener("pointerdown", handlePointerDown);
      stage.removeEventListener("pointermove", handlePointerMove);
      stage.removeEventListener("pointerup", handlePointerEnd);
      stage.removeEventListener("pointercancel", handlePointerEnd);
      disposeDrag = undefined;
    };
  }

  function enableStageResize() {
    const target = document.getElementById(STAGE_ELEMENT_ID);
    if (!target) return;

    const stage: HTMLElement = target;

    const handle = document.createElement("div");
    const handleSize = 24;
    Object.assign(handle.style, {
      position: "absolute",
      right: "0",
      bottom: "0",
      width: `${handleSize}px`,
      height: `${handleSize}px`,
      // 图标用白色加投影，保证在深浅背景上都清晰可见
      color: "rgba(255, 255, 255, 0.92)",
      filter: "drop-shadow(0 0 2px rgba(0, 0, 0, 0.55))",
      cursor: "nwse-resize",
      zIndex: "9999",
      display: "none",
      touchAction: "none",
    });
    render(createVNode(CaretDownRight24Regular), handle);
    const handleIcon = handle.querySelector("svg");
    if (handleIcon) {
      handleIcon.setAttribute("width", "100%");
      handleIcon.setAttribute("height", "100%");
      handleIcon.style.display = "block";
    }
    stage.append(handle);
    resizeHandle = handle;

    let pointerId = -1;
    let startX = 0;
    let startY = 0;
    let startScale = LIVE2D_LAYOUT_DEFAULTS.scale;
    let startWidth = 0;
    let startHeight = 0;
    let startLeft = 0;
    let startBottom = 0;
    // 是否真正调整过尺寸，避免单击手柄也触发保存
    let resized = false;

    function handlePointerDown(event: PointerEvent) {
      if (event.button !== 0 || pointerId !== -1 || !options.isReady()) return;

      // 阻止冒泡，避免同时触发舞台拖动
      event.stopPropagation();
      event.preventDefault();

      const rect = stage.getBoundingClientRect();
      pointerId = event.pointerId;
      startX = event.clientX;
      startY = event.clientY;
      startScale = options.getScale();
      startWidth = rect.width;
      startHeight = rect.height;
      startLeft = rect.left;
      startBottom = rect.bottom;
      resized = false;
      options.layout.beginAdjusting();
      handle.setPointerCapture(pointerId);
    }

    function handlePointerMove(event: PointerEvent) {
      if (event.pointerId !== pointerId) return;

      const offsetX = event.clientX - startX;
      const offsetY = event.clientY - startY;
      // 手柄在右下角，水平与垂直位移各占一半权重换算缩放比例
      const ratio = 1 + (offsetX / startWidth + offsetY / startHeight) / 2;
      // 同时限制不超出视口可容纳的最大尺寸
      const maxScale = Math.min(
        MAX_MODEL_SCALE,
        startScale * (window.innerWidth / startWidth),
        startScale * (window.innerHeight / startHeight),
      );
      const nextScale = Math.min(maxScale, Math.max(MIN_MODEL_SCALE, startScale * ratio));
      if (nextScale === options.getScale()) return;

      resized = true;
      options.setScale(nextScale);
      const scaleRatio = nextScale / startScale;
      const width = startWidth * scaleRatio;
      const height = startHeight * scaleRatio;

      const instance = options.getOml2d();
      instance?.setModelScale(nextScale);
      instance?.setStageStyle({ width, height });
      // 舞台尺寸变化后需要让渲染器同步画布分辨率，否则画面会被拉伸
      instance?.pixiApp?.resize();

      if (options.layout.edgeSnap.value) {
        // 保持左下角不动、底边贴住原位置，尺寸超出视口时向内收敛
        const left = Math.max(0, Math.min(startLeft, window.innerWidth - width));
        const top = Math.max(0, Math.min(startBottom - height, window.innerHeight - height));
        stage.style.left = `${left}px`;
        stage.style.top = `${top}px`;
      } else {
        // 关闭边缘吸附时保持左边界不动，允许尺寸变化后继续超出视口
        stage.style.left = `${startLeft}px`;
        stage.style.top = `${startBottom - height}px`;
      }
      stage.style.bottom = "auto";
      stage.style.right = "auto";
    }

    function handlePointerEnd(event: PointerEvent) {
      if (event.pointerId !== pointerId) return;

      pointerId = -1;
      options.layout.endAdjusting();
      if (handle.hasPointerCapture(event.pointerId)) {
        handle.releasePointerCapture(event.pointerId);
      }
      if (resized) options.layout.persistLayout();
    }

    handle.addEventListener("pointerdown", handlePointerDown);
    handle.addEventListener("pointermove", handlePointerMove);
    handle.addEventListener("pointerup", handlePointerEnd);
    handle.addEventListener("pointercancel", handlePointerEnd);

    updateResizeHandleVisibility();

    disposeResize = () => {
      handle.removeEventListener("pointerdown", handlePointerDown);
      handle.removeEventListener("pointermove", handlePointerMove);
      handle.removeEventListener("pointerup", handlePointerEnd);
      handle.removeEventListener("pointercancel", handlePointerEnd);
      render(null, handle);
      handle.remove();
      resizeHandle = undefined;
      disposeResize = undefined;
    };
  }

  function dispose() {
    disposeDrag?.();
    disposeResize?.();
    resizeMode.value = false;
  }

  return {
    resizeMode,
    setResizeMode,
    updateResizeHandleVisibility,
    enableStageDrag,
    enableStageResize,
    dispose,
  };
}
