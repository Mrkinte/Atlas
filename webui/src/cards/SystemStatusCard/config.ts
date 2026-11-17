export type SystemStatusVariant = "classic" | "minimal" | "ring" | "segments";

export type SystemStatusCardConfig = {
  title: string;
  variant: SystemStatusVariant;
  showMemory: boolean;
  showDisk: boolean;
  showLoad: boolean;
  showProcess: boolean;
  showUptime: boolean;
};

/** 与组件内的样式分支一一对应 */
export const SYSTEM_STATUS_VARIANTS: {
  label: string;
  value: SystemStatusVariant;
}[] = [
  { label: "经典", value: "classic" },
  { label: "极简", value: "minimal" },
  { label: "环形", value: "ring" },
  { label: "分段", value: "segments" },
];
