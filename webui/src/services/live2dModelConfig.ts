import { getLive2dModelConfig, updateLive2dModelConfig } from "@/apis/modules/live2d.ts";
import { extractErrorMessage } from "@/apis/errors.ts";
import {
  LIVE2D_LAYOUT_DEFAULTS,
  LIVE2D_LAYOUT_LIMITS,
  LIVE2D_OUTFIT_ID_MAX_LENGTH,
  LIVE2D_OUTFIT_MAX_COUNT,
  type Live2dOutfitSelection,
} from "@/config/default.ts";
import { demoMode } from "@/stores/demo.ts";
/** 自托管在 public/live2d，构建后随 wwwroot 发布 */
export const BUILT_IN_MODEL_PATH = "/live2d/大凤-毒苹果/dafeng_2.model3.json";

/** 对应后端 Live2DModelConfigResponse */
export interface Live2dModelConfig {
  modelPath: string;
  /** manifest motion id 或动作文件路径 */
  entryMotionId: string;
  /** manifest motion id 或动作文件路径 */
  idleMotionId: string;
  expression: string;
  outfits: Live2dOutfitSelection[];
  soundEnabled: boolean;
  scale: number;
  /** 0~1，相对可移动范围 */
  positionX: number;
  positionY: number;
  /** 按视口宽高比例；null 表示尚未记录 */
  freeRatioX: number | null;
  freeRatioY: number | null;
}

export type Live2dLayout = Pick<
  Live2dModelConfig,
  "scale" | "positionX" | "positionY" | "freeRatioX" | "freeRatioY"
>;

export function createEmptyLive2dModelConfig(modelPath: string): Live2dModelConfig {
  return {
    modelPath,
    entryMotionId: "",
    idleMotionId: "",
    expression: "",
    outfits: [],
    soundEnabled: true,
    ...LIVE2D_LAYOUT_DEFAULTS,
  };
}

/** 接口异常时返回空配置，不影响看板娘加载 */
export async function loadLive2dModelConfig(modelPath: string): Promise<Live2dModelConfig> {
  try {
    const { data, response } = await getLive2dModelConfig(modelPath);
    if (!response.ok) return createEmptyLive2dModelConfig(modelPath);

    return normalizeLive2dModelConfig(data, modelPath);
  } catch {
    return createEmptyLive2dModelConfig(modelPath);
  }
}

/** 成功返回 undefined，失败返回错误文案 */
export async function saveLive2dModelConfig(
  config: Live2dModelConfig,
): Promise<string | undefined> {
  if (demoMode.value) return undefined;

  try {
    const { error, response } = await updateLive2dModelConfig(
      normalizeLive2dModelConfig(config, config.modelPath),
    );

    if (!response.ok) {
      // ProblemDetails：401 提示登录，其余优先展示 detail
      return extractErrorMessage(error, "保存失败", response.status);
    }
    return undefined;
  } catch {
    return "网络异常，请稍后重试";
  }
}

/** 避免坏数据进入运行时或写回后端 */
export function normalizeLive2dModelConfig(
  input: unknown,
  fallbackModelPath = "",
): Live2dModelConfig {
  const data =
    typeof input === "object" && input !== null ? (input as Record<string, unknown>) : {};

  return {
    modelPath: readText(data.modelPath, 1024) || fallbackModelPath,
    entryMotionId: readText(data.entryMotionId, 256),
    idleMotionId: readText(data.idleMotionId, 256),
    expression: readText(data.expression, 128),
    outfits: normalizeOutfits(data.outfits),
    soundEnabled: data.soundEnabled !== false,
    scale: clampNumber(data.scale, LIVE2D_LAYOUT_LIMITS.scale, LIVE2D_LAYOUT_DEFAULTS.scale),
    positionX: clampNumber(
      data.positionX,
      LIVE2D_LAYOUT_LIMITS.position,
      LIVE2D_LAYOUT_DEFAULTS.positionX,
    ),
    positionY: clampNumber(
      data.positionY,
      LIVE2D_LAYOUT_LIMITS.position,
      LIVE2D_LAYOUT_DEFAULTS.positionY,
    ),
    freeRatioX: normalizeFreeRatio(data.freeRatioX),
    freeRatioY: normalizeFreeRatio(data.freeRatioY),
  };
}

function clampNumber(
  value: unknown,
  limits: { readonly min: number; readonly max: number },
  fallback: number,
): number {
  if (typeof value !== "number" || !Number.isFinite(value)) return fallback;
  return Math.min(limits.max, Math.max(limits.min, value));
}

/** 允许负值，缺失或非法值为 null */
function normalizeFreeRatio(value: unknown): number | null {
  if (typeof value !== "number" || !Number.isFinite(value)) return null;
  const { min, max } = LIVE2D_LAYOUT_LIMITS.freeRatio;
  return Math.min(max, Math.max(min, value));
}

function normalizeOutfits(value: unknown): Live2dOutfitSelection[] {
  if (!Array.isArray(value)) return [];

  const result: Live2dOutfitSelection[] = [];
  const groups = new Set<string>();

  for (const item of value) {
    if (typeof item !== "object" || item === null) continue;

    const { groupId, optionId } = item as Partial<Live2dOutfitSelection>;
    const group = readText(groupId, LIVE2D_OUTFIT_ID_MAX_LENGTH);
    const option = readText(optionId, LIVE2D_OUTFIT_ID_MAX_LENGTH);
    if (!group || !option || groups.has(group)) continue;

    groups.add(group);
    result.push({ groupId: group, optionId: option });
    if (result.length >= LIVE2D_OUTFIT_MAX_COUNT) break;
  }

  return result;
}

function readText(value: unknown, max: number): string {
  if (typeof value !== "string") return "";

  const text = value.trim().slice(0, max);
  // 刻意匹配控制字符用于拒绝非法输入，并非误用正则
  // eslint-disable-next-line no-control-regex
  return /[\u0000-\u001f\u007f]/.test(text) ? "" : text;
}
