export type CountdownCardConfig = {
  title: string;
  /** 格式 YYYY-MM-DD，空字符串表示未设置 */
  date: string;
  /** 生日、纪念日等按每年下一次发生日期计算 */
  repeatYearly: boolean;
  /** 支持带透明度，空字符串表示透明 */
  backgroundColor: string;
  backgroundImage: string;
};
