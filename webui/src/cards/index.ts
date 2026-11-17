import { registerCard } from "@/cards/registry";
import type { CardPlugin } from "@/cards/types";

const manifests = import.meta.glob<{ default: CardPlugin }>("/src/cards/*/manifest.ts", {
  eager: true,
});

for (const [path, mod] of Object.entries(manifests)) {
  try {
    registerCard(mod.default);
  } catch (err) {
    console.error(`[cardRegistry] 加载 ${path} 失败:`, err);
  }
}
