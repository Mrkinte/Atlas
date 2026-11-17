export type ClockCardVariant = "pixel" | "handwritten" | "jetbrains";

export type ClockCardConfig = {
  variant: ClockCardVariant;
  showSeconds: boolean;
  showDate: boolean;
  /** 关闭时显示 12 小时制与 AM/PM */
  hour24: boolean;
  /** 为空时跟随卡片文字颜色 */
  accentColor: string;
};
