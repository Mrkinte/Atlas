import type { CardRequired } from "@/cards/types.ts";
import { COL_WIDTH, MIN_GRID_MARGIN } from "@/config/default.ts";

export function gridMargin(containerWidth: number, cols: number): number {
  const raw = (containerWidth - cols * COL_WIDTH) / (cols + 1);
  return Math.max(MIN_GRID_MARGIN, Math.round(raw));
}

export function desktopGridContainerMinWidth(desktopCols: number): number {
  return desktopCols * COL_WIDTH + (desktopCols + 1) * MIN_GRID_MARGIN;
}

export function findNextPosition(cards: CardRequired[]) {
  const smBottom = cards.reduce((max, item) => Math.max(max, item.sm.y + item.sm.height), 0);
  const lgBottom = cards.reduce((max, item) => Math.max(max, item.lg.y + item.lg.height), 0);
  return { smY: smBottom, lgY: lgBottom };
}
