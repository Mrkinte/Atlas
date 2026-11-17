import type { CardPlugin } from "@/cards/types";

const registry = new Map<string, CardPlugin>();

export function registerCard(plugin: CardPlugin) {
  if (registry.has(plugin.name)) {
    console.warn(`[cardRegistry] 重复注册: ${plugin.name}，后者覆盖前者`);
  }
  registry.set(plugin.name, plugin);
}

export function getCardPlugin(name: string): CardPlugin | undefined {
  return registry.get(name);
}

export function listCardPlugins(): CardPlugin[] {
  return [...registry.values()];
}
