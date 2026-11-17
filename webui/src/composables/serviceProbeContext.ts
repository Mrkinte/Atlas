import type { ComputedRef, InjectionKey } from "vue";
import type { ServiceProbeEntry } from "@/services/serviceProbeScheduler.ts";

/** 所在格子的探测结果；未参与探测或非服务卡片时为 undefined */
export type ServiceProbeContext = ComputedRef<ServiceProbeEntry | undefined>;

/** 由 ServiceProbeProvider 提供，卡片组件按需读取，无需知道自己的 card id */
export const SERVICE_PROBE_KEY: InjectionKey<ServiceProbeContext> = Symbol("atlas-service-probe");
