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

/** 优先接在最后一行最右侧；放不下或会与跨行卡片重叠时另起一行 */
export function findAppendPosition(
  cards: CardRequired[],
  key: "sm" | "lg",
  width: number,
  height: number,
  cols: number,
) {
  if (cards.length === 0) return { x: 0, y: 0 };

  let rowY = 0;
  let bottom = 0;
  for (const card of cards) {
    const layout = card[key];
    rowY = Math.max(rowY, layout.y);
    bottom = Math.max(bottom, layout.y + layout.height);
  }

  const rightEdge = cards.reduce((max, card) => {
    const layout = card[key];
    return layout.y === rowY ? Math.max(max, layout.x + layout.width) : max;
  }, 0);

  // 跨行的高卡片可能占据最后一行右侧的空位
  const collides = cards.some((card) => {
    const layout = card[key];
    return (
      rightEdge < layout.x + layout.width &&
      rightEdge + width > layout.x &&
      rowY < layout.y + layout.height &&
      rowY + height > layout.y
    );
  });

  if (rightEdge + width <= cols && !collides) return { x: rightEdge, y: rowY };
  return { x: 0, y: bottom };
}
