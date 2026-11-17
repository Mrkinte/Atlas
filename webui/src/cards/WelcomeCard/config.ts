export type WelcomeCardConfig = {
  title: string;
  subtitle: string;
  /** 为空时跟随主题默认色 */
  titleColor: string;
  /** 为空时跟随主题默认色 */
  subtitleColor: string;
  align: "left" | "center" | "right";
};
