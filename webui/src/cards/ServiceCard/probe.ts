import type { ServiceCardConfig } from "@/cards/ServiceCard/config.ts";
import { isValidHttpUrl, resolveAutoServiceUrl } from "@/cards/ServiceCard/target.ts";

export const PROBE_TIMEOUT_MS = 5000;

/** 取中位数，削弱单次网络抖动 */
const PROBE_SAMPLES = 3;

/** aborted 表示主动取消，不用于展示 */
export type ProbeFailureReason = "timeout" | "unreachable" | "aborted";

export type ServiceProbeResult =
  { state: "online"; latencyMs: number } | { state: "offline"; reason: ProbeFailureReason };

/** 手动模式取第一个合法网址，即卡片默认打开的目标 */
export function resolveProbeTarget(config: Partial<ServiceCardConfig>): string | null {
  if (config.hostMode === "manual") {
    const urls = [config.url1, config.url2, config.url3]
      .map((url) => url?.trim() ?? "")
      .filter((url) => isValidHttpUrl(url));
    return urls[0] ?? null;
  }

  const autoUrl = resolveAutoServiceUrl(config.port, config.path, config.protocol);
  return isValidHttpUrl(autoUrl) ? autoUrl : null;
}

/** 避免代理或浏览器缓存导致耗时失真 */
function withCacheBuster(url: string): string {
  const target = new URL(url);
  target.searchParams.set("_atlas_probe", Math.random().toString(36).slice(2));
  return target.toString();
}

function median(values: number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)];
}

/** no-cors 不要求目标配置 CORS，但响应不可读，请求完成即视为可达 */
export async function probeService(url: string, signal?: AbortSignal): Promise<ServiceProbeResult> {
  const latencies: number[] = [];

  for (let index = 0; index < PROBE_SAMPLES; index += 1) {
    if (signal?.aborted) return { state: "offline", reason: "aborted" };

    const controller = new AbortController();
    const timer = window.setTimeout(() => controller.abort(), PROBE_TIMEOUT_MS);
    const abortFromOutside = () => controller.abort();
    signal?.addEventListener("abort", abortFromOutside, { once: true });

    const startedAt = performance.now();
    try {
      await fetch(withCacheBuster(url), {
        method: "HEAD",
        mode: "no-cors",
        cache: "no-store",
        credentials: "omit",
        redirect: "follow",
        signal: controller.signal,
      });
      latencies.push(performance.now() - startedAt);
    } catch {
      if (signal?.aborted) return { state: "offline", reason: "aborted" };
      return {
        state: "offline",
        reason: controller.signal.aborted ? "timeout" : "unreachable",
      };
    } finally {
      window.clearTimeout(timer);
      signal?.removeEventListener("abort", abortFromOutside);
    }
  }

  return { state: "online", latencyMs: Math.round(median(latencies)) };
}
