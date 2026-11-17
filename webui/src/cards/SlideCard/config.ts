export interface SlideItem {
  /** 与图库 ImageResponse.url 一致 */
  url: string;
  /** 只存于卡片配置，不回写图库 */
  title: string;
}

export type SlideObjectFit = "cover" | "contain";

export type SlideCardConfig = {
  slides: SlideItem[];
  /** 仅一张图片时不生效 */
  autoplay: boolean;
  interval: number;
  objectFit: SlideObjectFit;
  showTitle: boolean;
  showDots: boolean;
  showArrow: boolean;
  /** 为空时使用白色 */
  titleColor: string;
};
