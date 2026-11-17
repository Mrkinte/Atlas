import { describe, expect, it, vi } from "vitest";
import type { ServiceProbeResult } from "@/cards/ServiceCard/probe.ts";
import type { PlatformCardRequired } from "@/cards/types.ts";
import {
  createServiceProbeScheduler,
  type ProbeFn,
  type ServiceProbeEntry,
  type ServiceProbeSchedulerOptions,
} from "@/services/serviceProbeScheduler.ts";

interface Deferred {
  promise: Promise<ServiceProbeResult>;
  resolve: (result: ServiceProbeResult) => void;
  reject: (reason?: unknown) => void;
}

function createDeferred(): Deferred {
  let resolve!: (result: ServiceProbeResult) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<ServiceProbeResult>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

/** 调度器无内部定时器，排空一轮微任务即可 */
function flushAsync(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function makeCard(i: string | number): PlatformCardRequired {
  return {
    i,
    x: 0,
    y: 0,
    w: 1,
    h: 1,
    isVisible: true,
    mobileVisible: true,
    type: "Service",
    config: {},
  };
}

function createHarness(maxConcurrent?: number) {
  /** 按探测 URL 索引，可手动决议 */
  const pending = new Map<string, Deferred>();
  const probe = vi.fn<ProbeFn>((url) => {
    const deferred = createDeferred();
    pending.set(url, deferred);
    return deferred.promise;
  });
  const entries: Array<{ cardId: string; entry: ServiceProbeEntry }> = [];
  const removed: string[] = [];
  const targets = new Map<string, string>();
  const options: ServiceProbeSchedulerOptions = {
    resolveTarget: (card) => targets.get(String(card.i)) ?? null,
    probe,
    onEntry: (cardId, entry) => entries.push({ cardId, entry }),
    onRemove: (cardId) => removed.push(cardId),
    maxConcurrent,
  };
  return {
    scheduler: createServiceProbeScheduler(options),
    probe,
    pending,
    entries,
    removed,
    targets,
  };
}

describe("createServiceProbeScheduler", () => {
  it("sync 后先上报 probing，探测成功后上报 online 延迟", async () => {
    const h = createHarness();
    h.targets.set("a", "http://a.example");
    h.scheduler.sync([makeCard("a")]);

    expect(h.probe).toHaveBeenCalledTimes(1);
    expect(h.probe).toHaveBeenCalledWith("http://a.example", expect.any(AbortSignal));
    expect(h.entries).toEqual([{ cardId: "a", entry: { state: "probing" } }]);

    h.pending.get("http://a.example")!.resolve({
      state: "online",
      latencyMs: 42,
    });
    await flushAsync();

    expect(h.entries.at(-1)).toEqual({
      cardId: "a",
      entry: { state: "online", latencyMs: 42 },
    });
  });

  it("目标未变化时重复 sync 不重复探测", () => {
    const h = createHarness();
    h.targets.set("a", "http://a.example");
    h.scheduler.sync([makeCard("a")]);
    h.scheduler.sync([makeCard("a")]);

    expect(h.probe).toHaveBeenCalledTimes(1);
    expect(h.entries.filter((item) => item.entry.state === "probing")).toHaveLength(1);
  });

  it("探测抛错按 offline/unreachable 处理，不中断队列", async () => {
    const h = createHarness();
    h.targets.set("a", "http://a.example");
    h.probe.mockRejectedValueOnce(new Error("boom"));
    h.scheduler.sync([makeCard("a")]);

    await flushAsync();

    expect(h.entries.at(-1)).toEqual({
      cardId: "a",
      entry: { state: "offline", reason: "unreachable" },
    });
  });

  it("目标变化时取消旧探测，旧结果被丢弃、新结果生效", async () => {
    const h = createHarness();
    h.targets.set("a", "http://old.example");
    h.scheduler.sync([makeCard("a")]);
    const [, oldSignal] = h.probe.mock.calls[0];

    h.targets.set("a", "http://new.example");
    h.scheduler.sync([makeCard("a")]);

    expect(oldSignal?.aborted).toBe(true);
    expect(h.probe).toHaveBeenCalledTimes(2);
    const [newUrl] = h.probe.mock.calls[1];
    expect(newUrl).toBe("http://new.example");

    // 旧探测迟到的成功结果不上报
    h.pending.get("http://old.example")!.resolve({
      state: "online",
      latencyMs: 1,
    });
    await flushAsync();
    expect(h.entries.filter((item) => item.entry.state === "online")).toHaveLength(0);

    h.pending.get("http://new.example")!.resolve({
      state: "online",
      latencyMs: 7,
    });
    await flushAsync();
    const online = h.entries.filter((item) => item.entry.state === "online");
    expect(online).toHaveLength(1);
    expect(online[0]).toEqual({
      cardId: "a",
      entry: { state: "online", latencyMs: 7 },
    });
  });

  it("并发受 maxConcurrent 限制，空位释放后按 FIFO 继续排队", async () => {
    const h = createHarness(2);
    const ids = ["a", "b", "c", "d"];
    ids.forEach((id) => h.targets.set(id, `http://${id}.example`));
    h.scheduler.sync(ids.map((id) => makeCard(id)));

    expect(h.probe).toHaveBeenCalledTimes(2);
    expect(h.probe.mock.calls.map((call) => call[0])).toEqual([
      "http://a.example",
      "http://b.example",
    ]);

    h.pending.get("http://a.example")!.resolve({ state: "online", latencyMs: 5 });
    await flushAsync();
    expect(h.probe).toHaveBeenCalledTimes(3);
    expect(h.probe.mock.calls[2]?.[0]).toBe("http://c.example");

    h.pending.get("http://b.example")!.resolve({
      state: "offline",
      reason: "timeout",
    });
    h.pending.get("http://c.example")!.resolve({ state: "online", latencyMs: 6 });
    await flushAsync();
    expect(h.probe).toHaveBeenCalledTimes(4);
    expect(h.probe.mock.calls[3]?.[0]).toBe("http://d.example");

    h.pending.get("http://d.example")!.resolve({ state: "online", latencyMs: 8 });
    await flushAsync();

    const results = h.entries
      .filter((item) => item.entry.state !== "probing")
      .map((item) => [item.cardId, item.entry]);
    expect(results).toEqual([
      ["a", { state: "online", latencyMs: 5 }],
      ["b", { state: "offline", reason: "timeout" }],
      ["c", { state: "online", latencyMs: 6 }],
      ["d", { state: "online", latencyMs: 8 }],
    ]);
  });

  it("卡片移除时上报 onRemove 并取消进行中的探测，重新添加会再次探测", async () => {
    const h = createHarness();
    h.targets.set("a", "http://a.example");
    h.scheduler.sync([makeCard("a")]);
    const [, signal] = h.probe.mock.calls[0];

    h.scheduler.sync([]);
    expect(h.removed).toEqual(["a"]);
    expect(signal?.aborted).toBe(true);

    // 被移除卡片的迟到结果不再上报
    h.pending.get("http://a.example")!.resolve({
      state: "online",
      latencyMs: 1,
    });
    await flushAsync();
    expect(h.entries.filter((item) => item.entry.state === "online")).toHaveLength(0);

    // 移除会清空目标缓存，重新添加后再次探测
    h.scheduler.sync([makeCard("a")]);
    expect(h.probe).toHaveBeenCalledTimes(2);
  });

  it("resolveTarget 返回 null 的卡片不参与探测，也不触发 onRemove", () => {
    const h = createHarness();
    h.targets.set("y", "http://y.example");
    h.scheduler.sync([makeCard("x"), makeCard("y")]);

    expect(h.probe).toHaveBeenCalledTimes(1);
    expect(h.entries.map((item) => item.cardId)).toEqual(["y"]);

    h.scheduler.sync([]);
    expect(h.removed).toEqual(["y"]);
  });

  it("dispose 后中止进行中的探测、清空队列并忽略后续 sync", async () => {
    const h = createHarness(1);
    h.targets.set("a", "http://a.example");
    h.targets.set("b", "http://b.example");
    h.scheduler.sync([makeCard("a"), makeCard("b")]);

    expect(h.probe).toHaveBeenCalledTimes(1); // b 尚在队列中
    const [, signal] = h.probe.mock.calls[0];

    h.scheduler.dispose();
    expect(signal?.aborted).toBe(true);

    h.pending.get("http://a.example")!.resolve({
      state: "online",
      latencyMs: 1,
    });
    await flushAsync();
    expect(h.probe).toHaveBeenCalledTimes(1); // 队列中的 b 不会启动

    h.targets.set("c", "http://c.example");
    h.scheduler.sync([makeCard("c")]);
    expect(h.probe).toHaveBeenCalledTimes(1);
    expect(h.entries.filter((item) => item.entry.state !== "probing")).toHaveLength(0);
  });

  it("探测方主动返回 aborted 时归一化为 unreachable", async () => {
    const h = createHarness();
    h.targets.set("a", "http://a.example");
    h.probe.mockResolvedValueOnce({ state: "offline", reason: "aborted" });
    h.scheduler.sync([makeCard("a")]);

    await flushAsync();

    expect(h.entries.at(-1)).toEqual({
      cardId: "a",
      entry: { state: "offline", reason: "unreachable" },
    });
  });

  it("卡片 id 为数字时按字符串形式登记与移除", () => {
    const h = createHarness();
    h.targets.set("3", "http://n.example");
    h.scheduler.sync([makeCard(3)]);

    expect(h.probe).toHaveBeenCalledTimes(1);

    h.scheduler.sync([]);
    expect(h.removed).toEqual(["3"]);
  });
});
