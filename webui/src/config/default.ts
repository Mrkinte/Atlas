export type Platform = "mobile" | "desktop";

/** px */
export const ROW_HEIGHT = 64;
/** px，水平间距计算依赖该值 */
export const COL_WIDTH = 64;
/** px */
export const GRID_GAP_Y = 20;
/** px */
export const MIN_GRID_MARGIN = 20;

export const MOBILE_GRID_COLS = 4;
export const DESKTOP_GRID_COLS = 16;

export const CARD_GROUP_NAME_MAX_LENGTH = 32;

/** 所有分组栅格共用的 transfer group */
export const CARD_GROUP_TRANSFER_GROUP = "home";

/** grid-layout-plus 在 maxRows 无限时用 h=1 预校验缩放下限，minH > 1 的卡片会被判定非法 */
export const HOME_GRID_MAX_ROWS = 1000;

/** px */
export const DESKTOP_GRID_CONTAINER_MAX_WIDTH = 1364;

export const DEFAULT_SITE_TITLE = "Atlas";
export const SITE_TITLE_MAX_LENGTH = 64;

export const SITE_SUBTITLE_MAX_LENGTH = 120;
export const FOOTER_TEXT_MAX_LENGTH = 120;

/** 与后端 Live2DOutfitSelection 对应 */
export interface Live2dOutfitSelection {
  groupId: string;
  optionId: string;
}

export const DEFAULT_SETTINGS = {
  desktopCols: DESKTOP_GRID_COLS,
  desktopGridContainerMaxWidth: DESKTOP_GRID_CONTAINER_MAX_WIDTH,
  desktopGridFullWidth: false,
  siteTitle: DEFAULT_SITE_TITLE,
  /** 空字符串表示不显示 */
  siteSubtitle: "",
  /** 空字符串使用内置图标 */
  favicon: "",
  /** 页头标题左侧是否显示站点图标 */
  showHeaderLogo: true,
  /** 空字符串表示不显示 */
  footerText: "Powered by marjin",
  /** 空字符串表示不显示 */
  backgroundImage: "",
  /** px */
  backgroundBlur: 0,
  /** % */
  backgroundMask: 35,
  /** #rrggbb，空字符串使用 Naive UI 默认主色 */
  primaryColor: "",
  publicHardware: [] as string[],
  /** 关闭后所有人包括管理员都停止接收 */
  systemStatusEnabled: true,
  dockerEnabled: true,
  /** 空字符串按平台取默认路径 */
  dockerSocket: "",
  serviceProbeEnabled: true,
  /** 关闭后首页不加载 Live2D 代码 */
  live2DEnabled: false,
  /** 空字符串使用内置模型 */
  live2DModelPath: "",
};

/** 每个模型独立保存 */
export const LIVE2D_LAYOUT_DEFAULTS = {
  scale: 0.12,
  /** 0~1，相对可移动范围 */
  positionX: 0,
  positionY: 1,
  /** 按视口宽高比例保存，可超出 0~1；null 表示尚未记录 */
  freeRatioX: null as number | null,
  freeRatioY: null as number | null,
};

export const LIVE2D_LAYOUT_LIMITS = {
  scale: { min: 0.05, max: 0.5 },
  /** 0 靠左/上，1 靠右/下 */
  position: { min: 0, max: 1 },
  /** 按视口比例，仅限制绝对值上限 */
  freeRatio: { min: -100, max: 100 },
} as const;

export const LIVE2D_OUTFIT_ID_MAX_LENGTH = 64;

export const LIVE2D_OUTFIT_MAX_COUNT = 32;

/** 与后端 Live2dModelService.MaxArchiveSize 保持一致 */
export const LIVE2D_MODEL_MAX_SIZE_BYTES = 100 * 1024 * 1024;

export type Settings = typeof DEFAULT_SETTINGS;

/** 前端表单与数据校验共用 */
export const SETTINGS_LIMITS = {
  desktopCols: { min: 12, max: 24 },
  desktopGridContainerMaxWidth: {
    min: 1028,
    max: 4096,
  },
  backgroundBlur: { min: 0, max: 40 },
  backgroundMask: { min: 0, max: 100 },
} as const;
