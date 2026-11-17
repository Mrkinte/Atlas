export type AnalogClockConfig = {
  showSeconds: boolean;
  /** 关闭时秒针逐格跳动 */
  smoothSeconds: boolean;
  showNumbers: boolean;
  /** 为空时使用默认红色 */
  accentColor: string;
  /** 表盘底色不透明度 0-100，越低越能透出卡片与站点背景 */
  backgroundOpacity: number;
};
