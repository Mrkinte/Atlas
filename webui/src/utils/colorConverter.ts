/** 0–255 */
type Rgb = { r: number; g: number; b: number };

/** 对应 Naive UI common 主题变量 */
export type PrimaryThemeColors = {
  primaryColor: string;
  primaryColorHover: string;
  primaryColorPressed: string;
  primaryColorSuppl: string;
};

const WHITE: Rgb = { r: 255, g: 255, b: 255 };
const BLACK: Rgb = { r: 0, g: 0, b: 0 };

/** 支持 3/6 位，非法值返回空字符串 */
export function normalizeHexColor(value: unknown): string {
  if (typeof value !== "string") return "";
  const text = value.trim();
  if (/^#[0-9a-f]{6}$/i.test(text)) return text.toLowerCase();
  if (/^#[0-9a-f]{3}$/i.test(text)) {
    const [r, g, b] = text.slice(1);
    return `#${r}${r}${g}${g}${b}${b}`.toLowerCase();
  }
  return "";
}

function parseHex(color: string): Rgb {
  const text = normalizeHexColor(color);
  return {
    r: Number.parseInt(text.slice(1, 3), 16),
    g: Number.parseInt(text.slice(3, 5), 16),
    b: Number.parseInt(text.slice(5, 7), 16),
  };
}

function toHex({ r, g, b }: Rgb): string {
  return `#${[r, g, b].map((value) => Math.round(value).toString(16).padStart(2, "0")).join("")}`;
}

function mix(color: Rgb, target: Rgb, ratio: number): Rgb {
  return {
    r: color.r + (target.r - color.r) * ratio,
    g: color.g + (target.g - color.g) * ratio,
    b: color.b + (target.b - color.b) * ratio,
  };
}

/** 0–1，用于判断能否继续提亮或压暗 */
function lightness({ r, g, b }: Rgb): number {
  return (Math.max(r, g, b) + Math.min(r, g, b)) / 510;
}

/**
 * 按 Naive UI 默认色关系生成色阶：hover 提亮、pressed 压暗，
 * suppl 浅色主题取 hover、深色主题取主色；颜色过亮或过暗时反向调整
 */
export function buildPrimaryThemeColors(primary: string, dark: boolean): PrimaryThemeColors {
  const base = parseHex(primary);
  const baseLightness = lightness(base);
  const hover = baseLightness > 0.85 ? mix(base, BLACK, 0.12) : mix(base, WHITE, 0.16);
  const pressed = baseLightness < 0.15 ? mix(base, WHITE, 0.12) : mix(base, BLACK, 0.2);

  return {
    primaryColor: toHex(base),
    primaryColorHover: toHex(hover),
    primaryColorPressed: toHex(pressed),
    primaryColorSuppl: dark ? toHex(base) : toHex(hover),
  };
}
