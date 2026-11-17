/** oh-my-live2d 创建的舞台容器 ID */
const STAGE_ELEMENT_ID = "oml2d-stage";

/** 单像素回读的复用缓冲，避免每次采样都分配内存 */
const hitProbePixel = new Uint8Array(4);

/** 像素采样间隔：合并鼠标移动过程中的采样，避免每帧都触发 GPU 回读 */
const HIT_PROBE_INTERVAL = 60;

/** 画布回读能力的重试间隔与次数：模型淡入期间读数不可信，不能一次失败就永久放弃点透 */
const HIT_PROBE_RETRY_INTERVAL = 1000;
const HIT_PROBE_MAX_FAILURES = 5;

/**
 * 采样回调挂在 UTILITY(-50)，早于 Pixi 渲染器的 LOW(-25)，
 * 保证渲染完成且绘制缓冲被合成清空之前读取像素
 */
const HIT_PROBE_TICKER_PRIORITY = -50;

/** 右键复用采样结果时允许的位置偏差，超出视为光标已移动、不再复用 */
const HIT_CACHE_TOLERANCE = 2;

/** 采样只依赖渲染器上的 WebGL 上下文与 ticker */
export interface Live2dHitTestPixiApp {
  renderer?: { gl?: WebGLRenderingContext | WebGL2RenderingContext };
  ticker?: {
    add: (fn: () => void, context?: unknown, priority?: number) => unknown;
    remove: (fn: () => void, context?: unknown) => unknown;
  };
}

interface Live2dHitTestOptions {
  isReady: () => boolean;
  /** 拖动/缩放期间必须保持可交互 */
  isAdjusting: () => boolean;
  isResizeMode: () => boolean;
  /** 像素必须在 Pixi 渲染完成的同一帧内读取 */
  getPixiApp: () => Live2dHitTestPixiApp | undefined;
}

interface HitSample {
  clientX: number;
  clientY: number;
  opaque: boolean;
}

/**
 * 像素命中测试与透明区域点透：
 * 光标落在模型透明像素上时关闭舞台指针事件，让下方元素接收鼠标
 */
export function useLive2dHitTest(options: Live2dHitTestOptions) {
  /** 画布像素是否可回读；WebGL 未保留绘制缓冲时为 false，此时直接放弃点透 */
  let hitProbeReadable: boolean | undefined;

  /** 最近一次采样结果：右键菜单事件不在渲染帧内，只能复用采样结果 */
  let lastSample: HitSample | undefined;

  /**
   * 读取光标位置的像素透明度
   * 必须在渲染完成后的同一帧内调用，否则绘制缓冲可能已被清空
   */
  function isModelPixelAt(
    gl: WebGLRenderingContext | WebGL2RenderingContext,
    canvas: HTMLCanvasElement,
    clientX: number,
    clientY: number,
  ) {
    const rect = canvas.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return false;

    const x = Math.floor(((clientX - rect.left) / rect.width) * canvas.width);
    const y = Math.floor(((clientY - rect.top) / rect.height) * canvas.height);
    if (x < 0 || y < 0 || x >= canvas.width || y >= canvas.height) return false;

    // readPixels 以左下角为原点，需要把画布坐标翻转过来
    gl.readPixels(x, canvas.height - 1 - y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, hitProbePixel);
    return hitProbePixel[3] > 4;
  }

  /**
   * WebGL 未开启 preserveDrawingBuffer 时读到的都是透明像素
   * 取几个分散采样点，避免模型中心刚好透明造成误判
   */
  function isCanvasPixelReadable(
    gl: WebGLRenderingContext | WebGL2RenderingContext,
    canvas: HTMLCanvasElement,
  ): boolean {
    const rect = canvas.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return false;

    for (const ratioX of [0.5, 0.3, 0.7]) {
      for (const ratioY of [0.5, 0.3, 0.7]) {
        if (
          isModelPixelAt(
            gl,
            canvas,
            rect.left + rect.width * ratioX,
            rect.top + rect.height * ratioY,
          )
        ) {
          return true;
        }
      }
    }

    return false;
  }

  function enable(): () => void {
    const stageElement = document.getElementById(STAGE_ELEMENT_ID);
    const canvasElement = stageElement?.querySelector("canvas");
    const app = options.getPixiApp();
    const gl = app?.renderer?.gl;
    const ticker = app?.ticker;
    if (!stageElement || !canvasElement || !gl || !ticker) return () => {};

    const stage: HTMLElement = stageElement;
    const canvas: HTMLCanvasElement = canvasElement;
    // 函数声明内无法沿用外层的类型收窄，显式保存引用
    const glContext: WebGLRenderingContext | WebGL2RenderingContext = gl;
    const appTicker = ticker;

    let pointer: { clientX: number; clientY: number } | undefined;

    /** 节流采样时间戳：ticker 每帧都会触发，按间隔跳过 */
    let lastSampleAt = 0;
    let probeFailures = 0;
    let probeCheckedAt = 0;

    /** 判定并缓存画布回读能力；多次失败后才放弃，避免模型淡入期间误判 */
    function probeCanvasReadable(): boolean {
      if (hitProbeReadable !== undefined) return hitProbeReadable;

      const now = performance.now();
      if (now - probeCheckedAt < HIT_PROBE_RETRY_INTERVAL) return false;
      probeCheckedAt = now;

      if (isCanvasPixelReadable(glContext, canvas)) {
        hitProbeReadable = true;
        return true;
      }

      probeFailures += 1;
      if (probeFailures >= HIT_PROBE_MAX_FAILURES) hitProbeReadable = false;
      return false;
    }

    /** 在渲染完成后的同一帧采样，此时绘制缓冲还未清空 */
    function update() {
      const position = pointer;
      if (!position) return;

      // 拖动、缩放期间必须保持可交互，否则会打断正在进行的指针捕获
      if (options.isAdjusting() || options.isResizeMode()) {
        stage.style.pointerEvents = "auto";
        lastSample = undefined;
        return;
      }

      const rect = stage.getBoundingClientRect();
      const inside =
        position.clientX >= rect.left &&
        position.clientX <= rect.right &&
        position.clientY >= rect.top &&
        position.clientY <= rect.bottom;
      if (!inside) {
        stage.style.pointerEvents = "";
        lastSample = undefined;
        return;
      }

      // 模型尚未渲染时像素不可信，保持默认交互
      if (!options.isReady()) return;

      // 回读不可用时直接保持可交互，后续移动不再重复尝试读取像素
      if (hitProbeReadable === false) {
        stage.style.pointerEvents = "auto";
        lastSample = undefined;
        return;
      }

      if (isModelPixelAt(glContext, canvas, position.clientX, position.clientY)) {
        // 能读到非透明像素说明回读可用，缓存结果后不再做能力探测
        hitProbeReadable = true;
        lastSample = { ...position, opaque: true };
        stage.style.pointerEvents = "auto";
        return;
      }

      // 回读不可用时不能判成透明，否则看板娘会彻底不可交互
      // 采样结果不可信，右键菜单回退到「无法判定、直接接管」
      if (probeCanvasReadable()) {
        lastSample = { ...position, opaque: false };
        stage.style.pointerEvents = "none";
        return;
      }

      lastSample = undefined;
      stage.style.pointerEvents = "auto";
    }

    function tick() {
      if (!pointer) return;

      const now = performance.now();
      if (now - lastSampleAt < HIT_PROBE_INTERVAL) return;
      lastSampleAt = now;

      update();
    }

    function handleMove(event: PointerEvent) {
      // 触摸没有 hover，点透按拖动与缩放的显隐规则处理
      if (event.pointerType === "touch") return;

      pointer = { clientX: event.clientX, clientY: event.clientY };
    }

    window.addEventListener("pointermove", handleMove, { passive: true });
    appTicker.add(tick, undefined, HIT_PROBE_TICKER_PRIORITY);

    return () => {
      window.removeEventListener("pointermove", handleMove);
      appTicker.remove(tick);
      pointer = undefined;
      lastSample = undefined;
      stage.style.pointerEvents = "";
    };
  }

  /**
   * 复用最近一次采样结果判断坐标是否在模型上
   * 右键事件不在渲染帧内读不到像素，只能依赖 ticker 采样
   */
  function isPointerOnModel(clientX: number, clientY: number): boolean | undefined {
    const sample = lastSample;
    if (!sample) return undefined;

    const moved =
      Math.abs(sample.clientX - clientX) > HIT_CACHE_TOLERANCE ||
      Math.abs(sample.clientY - clientY) > HIT_CACHE_TOLERANCE;
    return moved ? undefined : sample.opaque;
  }

  return { isPointerOnModel, enable };
}
