/** oh-my-live2d 创建的舞台容器 ID，与 useLive2dHitTest 保持一致 */
const STAGE_ELEMENT_ID = "oml2d-stage";

/** 按下到抬起的位移超过该阈值视为拖动，不计入点击调试 */
const CLICK_MOVE_THRESHOLD = 4;

interface DebugPoint {
  x: number;
  y: number;
}

/** 只声明调试需要的读取接口 */
interface DebugCoreModelLike {
  getDrawableCount?: () => number;
  /** drawable id 即 Cubism 的 ArtMesh id */
  getDrawableIds?: () => string[];
  /** 三角形索引，用于精确判断坐标是否在 ArtMesh 网格内 */
  getDrawableVertexIndices?: (index: number) => Uint16Array | Uint32Array;
  getDrawableOpacity?: (index: number) => number;
  getDrawableDynamicFlagIsVisible?: (index: number) => boolean;
  /** 渲染顺序，数值越大越靠前，用于取命中的最上层 ArtMesh */
  getDrawableRenderOrders?: () => Int32Array;
}

interface DebugInternalModelLike {
  coreModel?: DebugCoreModelLike;
  /** model3.json 中声明的点击区域，用于把 drawable id 映射成可读名称 */
  settings?: { hitAreas?: Array<{ Id?: string; Name?: string }> };
  /** 把 drawable 顶点换算成模型像素坐标，与 toModelPosition 的结果同一坐标系 */
  getDrawableVertices?: (index: number) => Float32Array;
}

interface DebugModelLike {
  /** 客户端坐标 → 模型像素坐标，普通对象即可代替 PIXI.Point */
  toModelPosition?: (position: DebugPoint, result: DebugPoint) => DebugPoint;
  internalModel?: DebugInternalModelLike;
}

interface Live2dMeshDebugOptions {
  isReady: () => boolean;
  getModel: () => DebugModelLike | undefined;
}

interface Live2dMeshHit {
  index: number;
  id: string;
  /** model3.json 声明的点击区域名 */
  hitAreaName?: string;
}

/** 以重心坐标符号判断点是否在三角形内 */
function isPointInTriangle(
  x: number,
  y: number,
  ax: number,
  ay: number,
  bx: number,
  by: number,
  cx: number,
  cy: number,
): boolean {
  const d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
  const d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
  const d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);

  const hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
  const hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
  return !(hasNegative && hasPositive);
}

function isPointInMesh(
  vertices: Float32Array,
  indices: ArrayLike<number>,
  point: DebugPoint,
): boolean {
  for (let i = 0; i + 2 < indices.length; i += 3) {
    const a = indices[i] * 2;
    const b = indices[i + 1] * 2;
    const c = indices[i + 2] * 2;

    if (
      isPointInTriangle(
        point.x,
        point.y,
        vertices[a],
        vertices[a + 1],
        vertices[b],
        vertices[b + 1],
        vertices[c],
        vertices[c + 1],
      )
    ) {
      return true;
    }
  }

  return false;
}

/** 把客户端坐标换算成 Canvas 内的 CSS 像素坐标，与 pixi 的 event.global 一致 */
function toCanvasPoint(stage: HTMLElement, clientX: number, clientY: number): DebugPoint {
  const canvas = stage.querySelector("canvas");
  const rect = (canvas ?? stage).getBoundingClientRect();
  return { x: clientX - rect.left, y: clientY - rect.top };
}

function findMeshAt(model: DebugModelLike, canvasPoint: DebugPoint): Live2dMeshHit | undefined {
  const internal = model.internalModel;
  const core = internal?.coreModel;
  if (!internal?.getDrawableVertices || !model.toModelPosition) return undefined;
  if (!core?.getDrawableCount || !core.getDrawableIds) return undefined;
  if (!core.getDrawableVertexIndices) return undefined;

  const meshPoint = model.toModelPosition(canvasPoint, { x: 0, y: 0 });

  const count = core.getDrawableCount();
  const ids = core.getDrawableIds();
  const renderOrders = core.getDrawableRenderOrders?.();

  let hit: Live2dMeshHit | undefined;
  let topOrder = -Infinity;

  for (let index = 0; index < count; index += 1) {
    // 不可见或完全透明的 drawable 不参与命中
    const visible = core.getDrawableDynamicFlagIsVisible?.(index) ?? true;
    if (!visible) continue;
    if ((core.getDrawableOpacity?.(index) ?? 1) <= 0) continue;

    const vertices = internal.getDrawableVertices(index);
    const indices = core.getDrawableVertexIndices(index);
    if (!vertices || !indices) continue;
    if (!isPointInMesh(vertices, indices, meshPoint)) continue;

    // 渲染顺序越大越靠前；取不到时按下标兜底
    const order = renderOrders?.[index] ?? index;
    if (order <= topOrder) continue;

    topOrder = order;
    hit = { index, id: ids[index] ?? String(index) };
  }

  if (!hit) return undefined;

  const hitAreaName = internal.settings?.hitAreas?.find((area) => area.Id === hit.id)?.Name;
  return hitAreaName ? { ...hit, hitAreaName } : hit;
}

/**
 * 开发环境专用的 ArtMesh 点击调试：
 * 左键单击时输出命中的 ArtMesh，生产构建不注册监听
 */
export function useLive2dMeshDebug(options: Live2dMeshDebugOptions) {
  function enable(): () => void {
    if (!import.meta.env.DEV) return () => {};

    const target = document.getElementById(STAGE_ELEMENT_ID);
    if (!target) return () => {};

    // 事件回调中无法沿用外层的类型收窄，显式保存为非空引用
    const stage: HTMLElement = target;

    let pointerId = -1;
    let downX = 0;
    let downY = 0;

    function handlePointerDown(event: PointerEvent) {
      if (event.button !== 0 || event.pointerType === "touch") return;

      pointerId = event.pointerId;
      downX = event.clientX;
      downY = event.clientY;
    }

    function handlePointerUp(event: PointerEvent) {
      if (event.button !== 0 || event.pointerType === "touch") return;
      if (event.pointerId !== pointerId) return;

      pointerId = -1;
      // 拖动模型时也会产生 pointerup，位移超过阈值就不算点击
      if (
        Math.abs(event.clientX - downX) > CLICK_MOVE_THRESHOLD ||
        Math.abs(event.clientY - downY) > CLICK_MOVE_THRESHOLD
      ) {
        return;
      }
      if (!options.isReady()) return;

      const model = options.getModel();
      if (!model) return;

      const canvasPoint = toCanvasPoint(stage, event.clientX, event.clientY);
      const hit = findMeshAt(model, canvasPoint);
      if (!hit) {
        console.info("[Live2D] 左键未命中任何 ArtMesh", canvasPoint);
        return;
      }

      console.info(`[Live2D] 左键命中 ArtMesh: ${hit.id}`, {
        index: hit.index,
        id: hit.id,
        hitArea: hit.hitAreaName,
      });
    }

    stage.addEventListener("pointerdown", handlePointerDown, true);
    stage.addEventListener("pointerup", handlePointerUp, true);

    return () => {
      stage.removeEventListener("pointerdown", handlePointerDown, true);
      stage.removeEventListener("pointerup", handlePointerUp, true);
    };
  }

  return { enable };
}
