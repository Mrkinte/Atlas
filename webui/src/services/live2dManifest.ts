/**
 * 展示清单与 .model3.json 同目录，命名 <model-stem>.live2d.json；
 * 不改动原始模型文件，删除清单即回到按文件名展示
 */

import { fetchLive2dAssetJson } from "@/apis/modules/live2d.ts";

/** 用动作组名与文件定位 model3.json 中的动作，展示名由清单提供 */
export interface Live2dManifestMotion {
  id: string;
  name: string;
  /** 缺省时按文件全局查找 */
  group?: string;
  file: string;
  /** 气泡台词 */
  text?: string;
  /** 毫秒 */
  duration?: number;
}

/** 组名显示在右键菜单 */
export interface Live2dManifestMotionGroup {
  id: string;
  name: string;
  motions: string[];
}

/** runtimeName 对应 model3.json 的 Expressions[].Name */
export interface Live2dManifestExpression {
  id: string;
  name: string;
  runtimeName: string;
}

/** 参数 id → 绝对目标值，逐项写入模型 */
export interface Live2dManifestWardrobeOption {
  id: string;
  name: string;
  /** 单参数清单也归一化为一项 */
  values: Record<string, number>;
}

/**
 * 同组选项互斥，不同组可同时生效；
 * 旧清单只有 parameter 一个参数
 */
export interface Live2dManifestWardrobeGroup {
  id: string;
  name: string;
  /** 兼容旧清单，取 parameters[0] */
  parameter: string;
  /** 至少包含 parameter */
  parameters: string[];
  options: Live2dManifestWardrobeOption[];
}

/** 站点配置里没有选择时使用 */
export interface Live2dManifestDefaults {
  entryMotion?: string;
  /** runtimeName 或 expression id */
  expression?: string;
  wardrobe: Record<string, string>;
}

/** 无展示清单的模型直接从 model3.json 读取 */
export interface Live2dRuntimeMotionOption {
  group: string;
  index: number;
  file: string;
  name: string;
}

export interface Live2dRuntimeExpressionOption {
  runtimeName: string;
  name: string;
}

export interface Live2dRuntimeOptions {
  motions: Live2dRuntimeMotionOption[];
  expressions: Live2dRuntimeExpressionOption[];
}

export interface Live2dManifest {
  name?: string;
  /** 毫秒，台词未单独配置 duration 时使用 */
  defaultDuration?: number;
  motions: Live2dManifestMotion[];
  motionGroups: Live2dManifestMotionGroup[];
  expressions: Live2dManifestExpression[];
  /**
   * 声明空数组表示隐藏模型自带表情；
   * 未声明则回退到运行时表情
   */
  expressionsDeclared: boolean;
  wardrobe: Live2dManifestWardrobeGroup[];
  defaults: Live2dManifestDefaults;
}

const SUPPORTED_FORMATS = "atlas-live2d-manifest";

const SUPPORTED_VERSION = 1;

/** 避免异常清单撑大菜单或写入配置 */
const ID_MAX_LENGTH = 64;
const NAME_MAX_LENGTH = 128;
const FILE_MAX_LENGTH = 1024;
const TEXT_MAX_LENGTH = 512;

/** <stem>.model3.json → <stem>.live2d.json；非标准命名返回 undefined */
export function resolveLive2dManifestUrl(modelPath: string): string | undefined {
  const url = new URL(modelPath, document.baseURI);
  if (!/\.model3\.json$/i.test(url.pathname)) return undefined;

  url.pathname = url.pathname.replace(/\.model3\.json$/i, ".live2d.json");
  url.search = "";
  url.hash = "";
  return url.toString();
}

export async function loadLive2dManifest(modelPath: string): Promise<Live2dManifest | undefined> {
  const url = resolveLive2dManifestUrl(modelPath);
  if (!url) return undefined;

  // 清单可能在调试中反复修改，每次回源确认
  const data = await fetchLive2dAssetJson(url);
  if (data === undefined) return undefined;

  const manifest = parseManifest(data);
  if (!manifest) console.warn("[Live2D] 展示清单内容不合法:", url);
  return manifest;
}

/** 动作以文件路径保存，避免依赖易变的组内下标 */
export async function loadLive2dRuntimeOptions(modelPath: string): Promise<Live2dRuntimeOptions> {
  const empty: Live2dRuntimeOptions = { motions: [], expressions: [] };

  const data = await fetchLive2dAssetJson(new URL(modelPath, document.baseURI));
  if (data === undefined) return empty;
  if (typeof data !== "object" || data === null) return empty;

  const file = data as Record<string, unknown>;
  const references =
    typeof file.FileReferences === "object" && file.FileReferences !== null
      ? (file.FileReferences as Record<string, unknown>)
      : {};

  const motions: Live2dRuntimeMotionOption[] = [];
  const groups =
    typeof references.Motions === "object" && references.Motions !== null
      ? (references.Motions as Record<string, unknown>)
      : {};

  for (const [group, entries] of Object.entries(groups)) {
    if (!Array.isArray(entries)) continue;

    entries.forEach((entry, index) => {
      if (typeof entry !== "object" || entry === null) return;

      const motionFile = readText((entry as Record<string, unknown>).File, FILE_MAX_LENGTH);
      if (!motionFile) return;

      motions.push({
        group,
        index,
        file: motionFile,
        name:
          motionFile
            .split("/")
            .pop()
            ?.replace(/\.motion3\.json$/i, "") ?? motionFile,
      });
    });
  }

  const expressions: Live2dRuntimeExpressionOption[] = [];
  if (Array.isArray(references.Expressions)) {
    for (const entry of references.Expressions) {
      if (typeof entry !== "object" || entry === null) continue;

      const name = readText((entry as Record<string, unknown>).Name, NAME_MAX_LENGTH);
      if (!name) continue;

      expressions.push({ runtimeName: name, name });
    }
  }

  return { motions, expressions };
}

/** 坏条目直接跳过，避免一条写错影响整份清单 */
function parseManifest(data: unknown): Live2dManifest | undefined {
  if (typeof data !== "object" || data === null) return undefined;

  const file = data as Record<string, unknown>;
  if (
    typeof file.format !== "string" ||
    file.format !== SUPPORTED_FORMATS ||
    file.version !== SUPPORTED_VERSION
  ) {
    return undefined;
  }

  const motions = parseMotions(file.motions);
  const motionIds = new Set(motions.map((motion) => motion.id));
  const motionGroups = parseMotionGroups(file.motionGroups, motionIds);
  const expressions = parseExpressions(file.expressions);
  const wardrobe = parseWardrobe(file.wardrobe);

  const defaults = parseDefaults(file.defaults, motions, expressions, wardrobe);

  return {
    name: readText(file.name, NAME_MAX_LENGTH) || undefined,
    defaultDuration: isDuration(file.defaultDuration) ? file.defaultDuration : undefined,
    motions,
    motionGroups,
    expressions,
    expressionsDeclared: Array.isArray(file.expressions),
    wardrobe,
    defaults,
  };
}

function parseMotions(value: unknown): Live2dManifestMotion[] {
  if (!Array.isArray(value)) return [];

  const result: Live2dManifestMotion[] = [];
  const ids = new Set<string>();

  for (const item of value) {
    if (typeof item !== "object" || item === null) continue;

    const raw = item as Record<string, unknown>;
    const id = readId(raw.id);
    const file = readText(raw.file, FILE_MAX_LENGTH);
    if (!id || !file || ids.has(id)) continue;

    ids.add(id);
    result.push({
      id,
      name: readText(raw.name, NAME_MAX_LENGTH) || id,
      group: readText(raw.group, NAME_MAX_LENGTH) || undefined,
      file,
      text: readText(raw.text, TEXT_MAX_LENGTH) || undefined,
      duration: isDuration(raw.duration) ? raw.duration : undefined,
    });
  }

  return result;
}

function parseMotionGroups(value: unknown, motionIds: Set<string>): Live2dManifestMotionGroup[] {
  if (!Array.isArray(value)) return [];

  const result: Live2dManifestMotionGroup[] = [];
  const ids = new Set<string>();

  for (const item of value) {
    if (typeof item !== "object" || item === null) continue;

    const raw = item as Record<string, unknown>;
    const id = readId(raw.id);
    if (!id || ids.has(id)) continue;

    const motions = Array.isArray(raw.motions)
      ? raw.motions.filter(
          (motionId): motionId is string => typeof motionId === "string" && motionIds.has(motionId),
        )
      : [];
    if (motions.length === 0) continue;

    ids.add(id);
    result.push({
      id,
      name: readText(raw.name, NAME_MAX_LENGTH) || id,
      motions: [...new Set(motions)],
    });
  }

  return result;
}

function parseExpressions(value: unknown): Live2dManifestExpression[] {
  if (!Array.isArray(value)) return [];

  const result: Live2dManifestExpression[] = [];
  const ids = new Set<string>();
  const runtimeNames = new Set<string>();

  for (const item of value) {
    if (typeof item !== "object" || item === null) continue;

    const raw = item as Record<string, unknown>;
    const id = readId(raw.id);
    const runtimeName = readText(raw.runtimeName, NAME_MAX_LENGTH);
    if (!id || !runtimeName) continue;
    if (ids.has(id) || runtimeNames.has(runtimeName)) continue;

    ids.add(id);
    runtimeNames.add(runtimeName);
    result.push({
      id,
      name: readText(raw.name, NAME_MAX_LENGTH) || runtimeName,
      runtimeName,
    });
  }

  return result;
}

function parseWardrobe(value: unknown): Live2dManifestWardrobeGroup[] {
  if (!Array.isArray(value)) return [];

  const result: Live2dManifestWardrobeGroup[] = [];
  const ids = new Set<string>();
  // 同一参数只能属于一个组，否则多组互相覆盖；按参数集合判重
  const parameters = new Set<string>();

  for (const item of value) {
    if (typeof item !== "object" || item === null) continue;

    const raw = item as Record<string, unknown>;
    const id = readId(raw.id);
    const groupParameters = parseGroupParameters(raw);
    if (!id || groupParameters.length === 0) continue;
    if (ids.has(id) || groupParameters.some((parameter) => parameters.has(parameter))) {
      continue;
    }

    const options = parseWardrobeOptions(raw.options, groupParameters);
    if (options.length === 0) continue;

    ids.add(id);
    groupParameters.forEach((parameter) => parameters.add(parameter));
    result.push({
      id,
      name: readText(raw.name, NAME_MAX_LENGTH) || id,
      parameter: groupParameters[0],
      parameters: groupParameters,
      options,
    });
  }

  return result;
}

function parseGroupParameters(raw: Record<string, unknown>): string[] {
  const list = Array.isArray(raw.parameters)
    ? raw.parameters.map((item) => readText(item, ID_MAX_LENGTH)).filter((item) => item !== "")
    : [];
  if (list.length > 0) return [...new Set(list)];

  const parameter = readText(raw.parameter, ID_MAX_LENGTH);
  return parameter ? [parameter] : [];
}

function parseWardrobeOptions(
  value: unknown,
  groupParameters: string[],
): Live2dManifestWardrobeOption[] {
  if (!Array.isArray(value)) return [];

  const result: Live2dManifestWardrobeOption[] = [];
  const ids = new Set<string>();
  const allowed = new Set(groupParameters);

  for (const item of value) {
    if (typeof item !== "object" || item === null) continue;

    const raw = item as Record<string, unknown>;
    const id = readId(raw.id);
    if (!id || ids.has(id)) continue;

    const values: Record<string, number> = {};
    if (typeof raw.values === "object" && raw.values !== null) {
      for (const [parameter, target] of Object.entries(raw.values as Record<string, unknown>)) {
        if (!allowed.has(parameter)) continue;
        if (typeof target !== "number" || !Number.isFinite(target)) continue;
        values[parameter] = target;
      }
    }

    // 旧格式只有一个 value；新格式缺主参数值时也用它补全
    if (
      !(groupParameters[0] in values) &&
      typeof raw.value === "number" &&
      Number.isFinite(raw.value)
    ) {
      values[groupParameters[0]] = raw.value;
    }
    if (Object.keys(values).length === 0) continue;

    ids.add(id);
    result.push({
      id,
      name: readText(raw.name, NAME_MAX_LENGTH) || id,
      values,
    });
  }

  return result;
}

function parseDefaults(
  value: unknown,
  motions: Live2dManifestMotion[],
  expressions: Live2dManifestExpression[],
  wardrobe: Live2dManifestWardrobeGroup[],
): Live2dManifestDefaults {
  const wardrobeDefaults: Record<string, string> = {};
  if (typeof value !== "object" || value === null) {
    return { wardrobe: wardrobeDefaults };
  }

  const raw = value as Record<string, unknown>;
  const entryMotion =
    typeof raw.entryMotion === "string" && motions.some((motion) => motion.id === raw.entryMotion)
      ? raw.entryMotion
      : undefined;

  let expression: string | undefined;
  if (typeof raw.expression === "string") {
    const text = raw.expression.trim();
    const target = expressions.find((item) => item.id === text || item.runtimeName === text);
    expression = target?.runtimeName;
  }

  if (typeof raw.wardrobe === "object" && raw.wardrobe !== null) {
    for (const [groupId, optionId] of Object.entries(raw.wardrobe as Record<string, unknown>)) {
      if (typeof optionId !== "string") continue;

      const group = wardrobe.find((item) => item.id === groupId);
      if (!group?.options.some((option) => option.id === optionId)) continue;

      wardrobeDefaults[groupId] = optionId;
    }
  }

  return { entryMotion, expression, wardrobe: wardrobeDefaults };
}

function readId(value: unknown): string {
  return readText(value, ID_MAX_LENGTH);
}

function readText(value: unknown, max: number): string {
  if (typeof value !== "string") return "";

  const text = value.trim().slice(0, max);
  // 刻意匹配控制字符用于拒绝非法输入，并非误用正则
  // eslint-disable-next-line no-control-regex
  return /[\u0000-\u001f\u007f]/.test(text) ? "" : text;
}

function isDuration(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value) && value > 0;
}
