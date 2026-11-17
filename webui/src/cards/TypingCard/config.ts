/** 字体已打包在 src/assets/fonts */
export const TYPING_CARD_FONT_OPTIONS = [
  { label: "默认字体", value: "default" },
  { label: "Electrolize", value: "electrolize" },
  { label: "手写体（大梦落神赋）", value: "dameng" },
  { label: "JetBrains Mono", value: "jetbrains" },
  { label: "像素字体（Press Start 2P）", value: "pixel" },
] as const;

export type TypingCardFont = (typeof TYPING_CARD_FONT_OPTIONS)[number]["value"];

export type TypingCardConfig = {
  text: string;
  /** 为空时跟随卡片文字颜色 */
  color: string;
  fontSize: number;
  fontFamily: TypingCardFont;
  /** 毫秒/字符，越小越快 */
  typingSpeed: number;
  loop: boolean;
  /** 一轮打完后到下一轮开始的等待时间，毫秒 */
  loopInterval: number;
};
