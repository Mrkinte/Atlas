import type { PlatformCardRequired } from "@/cards/types.ts";
import type { ProbeFailureReason, ServiceProbeResult } from "@/cards/ServiceCard/probe.ts";

export type ServiceProbeEntry =
  | { state: "probing" }
  | { state: "online"; latencyMs: number }
  | { state: "offline"; reason: Exclude<ProbeFailureReason, "aborted"> };

/** 可注入替换以便测试 */
export type ProbeFn = (url: string, signal?: AbortSignal) => Promise<ServiceProbeResult>;

/** 避免一次性建立过多连接 */
const MAX_CONCURRENT_PROBES = 4;

export interface ServiceProbeSchedulerOptions {
  /** 返回 null 表示该卡片不参与探测 */
  resolveTarget: (card: PlatformCardRequired) => string | null;
  probe: ProbeFn;
  maxConcurrent?: number;
  onEntry: (cardId: string, entry: ServiceProbeEntry) => void;
  onRemove: (cardId: string) => void;
}

export interface ServiceProbeScheduler {
  /** 只探测新增或目标变化的卡片 */
  sync(cards: readonly PlatformCardRequired[]): void;
  dispose(): void;
}

/** 不依赖 Vue 渲染，探测目标与探测方式由调用方注入 */
export function createServiceProbeScheduler(
  options: ServiceProbeSchedulerOptions,
): ServiceProbeScheduler {
  const maxConcurrent = options.maxConcurrent ?? MAX_CONCURRENT_PROBES;
  /** 目标未变化时不重复探测 */
  const probedTargets = new Map<string, string>();
  /** 重新探测时旧结果直接丢弃 */
  const tokens = new Map<string, number>();
  const controllers = new Map<string, AbortController>();
  const queue: Array<() => Promise<void>> = [];
  let activeCount = 0;
  let disposed = false;

  function drainQueue() {
    if (disposed) return;

    while (activeCount < maxConcurrent && queue.length > 0) {
      const task = queue.shift()!;
      activeCount += 1;
      void task().finally(() => {
        activeCount -= 1;
        drainQueue();
      });
    }
  }

  function startProbe(cardId: string, target: string) {
    const token = (tokens.get(cardId) ?? 0) + 1;
    tokens.set(cardId, token);

    controllers.get(cardId)?.abort();

    const controller = new AbortController();
    controllers.set(cardId, controller);
    options.onEntry(cardId, { state: "probing" });

    queue.push(async () => {
      let result: ServiceProbeResult;
      try {
        result = await options.probe(target, controller.signal);
      } catch {
        // 注入的探测实现抛错时按不可达处理，避免中断队列
        result = { state: "offline", reason: "unreachable" };
      }

      if (disposed || tokens.get(cardId) !== token || controller.signal.aborted) {
        return;
      }

      controllers.delete(cardId);
      options.onEntry(
        cardId,
        result.state === "online"
          ? { state: "online", latencyMs: result.latencyMs }
          : {
              state: "offline",
              reason: result.reason === "aborted" ? "unreachable" : result.reason,
            },
      );
    });
    drainQueue();
  }

  function removeProbe(cardId: string) {
    controllers.get(cardId)?.abort();
    controllers.delete(cardId);
    probedTargets.delete(cardId);
    tokens.delete(cardId);
    options.onRemove(cardId);
  }

  function sync(cards: readonly PlatformCardRequired[]) {
    if (disposed) return;

    const currentIds = new Set<string>();

    for (const card of cards) {
      const cardId = String(card.i);
      const target = options.resolveTarget(card);
      if (!target) continue;

      currentIds.add(cardId);
      if (probedTargets.get(cardId) === target) continue;

      probedTargets.set(cardId, target);
      startProbe(cardId, target);
    }

    for (const cardId of [...probedTargets.keys()]) {
      if (currentIds.has(cardId)) continue;
      removeProbe(cardId);
    }
  }

  function dispose() {
    disposed = true;
    queue.length = 0;
    for (const controller of controllers.values()) controller.abort();
    controllers.clear();
  }

  return { sync, dispose };
}
