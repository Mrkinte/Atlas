import { describe, expect, it } from "vitest";
import type { CardRequired } from "@/cards/types.ts";
import {
  desktopGridContainerMinWidth,
  findNextPosition,
  gridMargin,
} from "@/utils/cardLayoutCalculate.ts";

/** 与 config/default.ts 保持一致 */
const COL_WIDTH = 64;
const MIN_GRID_MARGIN = 20;

function makeCard(
  id: string,
  sm: { y: number; height: number },
  lg: { y: number; height: number },
): CardRequired {
  return {
    id,
    sm: { x: 0, y: sm.y, width: 1, height: sm.height },
    lg: { x: 0, y: lg.y, width: 1, height: lg.height },
    minWidth: 1,
    minHeight: 1,
    isVisible: true,
    mobileVisible: true,
    type: "Service",
    config: {},
  };
}

describe("gridMargin", () => {
  it("桌面端默认参数（1028px、12 列）恰好取下限值", () => {
    // (1028 - 12 * 64) / 13 = 20
    expect(gridMargin(1028, 12)).toBe(20);
  });

  it("移动端典型宽度（375px、4 列）四舍五入", () => {
    // (375 - 4 * 64) / 5 = 23.8 → 24
    expect(gridMargin(375, 4)).toBe(24);
  });

  it("宽容器少列数时返回实际计算值", () => {
    expect(gridMargin(1000, 1)).toBe((1000 - COL_WIDTH) / 2);
  });

  it("小数结果四舍五入", () => {
    // (105 - 64) / 2 = 20.5 → 21
    expect(gridMargin(105, 1)).toBe(21);
  });

  it("计算值低于下限时钳制到 MIN_GRID_MARGIN", () => {
    // (300 - 4 * 64) / 5 = 8.8 → 9 → 钳制为 20
    expect(gridMargin(300, 4)).toBe(MIN_GRID_MARGIN);
  });

  it("容器过窄出现负值时同样钳制到下限", () => {
    expect(gridMargin(0, 12)).toBe(MIN_GRID_MARGIN);
  });
});

describe("desktopGridContainerMinWidth", () => {
  it("12 列对应桌面端容器默认宽度", () => {
    expect(desktopGridContainerMinWidth(12)).toBe(1028);
  });

  it("列数线性换算：4 列与 1 列", () => {
    expect(desktopGridContainerMinWidth(4)).toBe(4 * COL_WIDTH + 5 * MIN_GRID_MARGIN);
    expect(desktopGridContainerMinWidth(1)).toBe(104);
  });
});

describe("findNextPosition", () => {
  it("无卡片时从 0 开始", () => {
    expect(findNextPosition([])).toEqual({ smY: 0, lgY: 0 });
  });

  it("单卡片取 y + height", () => {
    const cards = [makeCard("a", { y: 0, height: 2 }, { y: 1, height: 3 })];
    expect(findNextPosition(cards)).toEqual({ smY: 2, lgY: 4 });
  });

  it("多卡片时两个平台各自取最大底边", () => {
    const cards = [
      makeCard("a", { y: 0, height: 2 }, { y: 1, height: 3 }),
      makeCard("b", { y: 5, height: 1 }, { y: 0, height: 2 }),
    ];
    // sm：max(0+2, 5+1) = 6；lg：max(1+3, 0+2) = 4
    expect(findNextPosition(cards)).toEqual({ smY: 6, lgY: 4 });
  });

  it("同起点卡片取更高者", () => {
    const cards = [
      makeCard("a", { y: 2, height: 3 }, { y: 0, height: 1 }),
      makeCard("b", { y: 2, height: 5 }, { y: 0, height: 1 }),
    ];
    expect(findNextPosition(cards)).toEqual({ smY: 7, lgY: 1 });
  });
});
