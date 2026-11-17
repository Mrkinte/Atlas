import { onBeforeUnmount, reactive, watch } from "vue";
import type { PlatformCardRequired } from "@/cards/types.ts";
import { getCardPlugin } from "@/cards/registry.ts";
import { probeService } from "@/cards/ServiceCard/probe.ts";
import { settings } from "@/stores/settings.ts";
import {
  createServiceProbeScheduler,
  type ServiceProbeEntry,
} from "@/services/serviceProbeScheduler.ts";

/**
 * 首页服务探测，把调度器接到 Vue 响应式状态与生命周期上
 * 探测目标由插件注册的 probeTarget 声明
 */
export function useServiceProbe(cards: () => PlatformCardRequired[]) {
  const entries = reactive(new Map<string, ServiceProbeEntry>());

  const scheduler = createServiceProbeScheduler({
    resolveTarget: (card) => getCardPlugin(card.type)?.probeTarget?.(card.config) ?? null,
    probe: probeService,
    onEntry: (cardId, entry) => entries.set(cardId, entry),
    onRemove: (cardId) => entries.delete(cardId),
  });

  watch(
    [cards, () => settings.serviceProbeEnabled],
    () => scheduler.sync(settings.serviceProbeEnabled ? cards() : []),
    { immediate: true },
  );

  onBeforeUnmount(() => scheduler.dispose());

  return { entries };
}
