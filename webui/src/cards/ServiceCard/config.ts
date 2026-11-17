export type HostMode = "auto" | "manual";

/** auto 表示跟随当前网址协议 */
export type ProtocolMode = "auto" | "https" | "http";

export type ServiceCardConfig = {
  name: string;
  description?: string;
  hostMode: HostMode;
  protocol?: ProtocolMode;
  /** 自动模式 */
  port?: number | null;
  /** 自动模式 */
  path?: string;
  /** 手动模式 */
  url1?: string;
  /** 手动模式 */
  url2?: string;
  /** 手动模式 */
  url3?: string;
  icon: string;
  openMethod: string;
};
