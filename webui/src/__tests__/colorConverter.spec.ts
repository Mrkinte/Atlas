import { describe, expect, it } from "vitest";
import { buildPrimaryThemeColors, normalizeHexColor } from "@/utils/colorConverter.ts";

describe("normalizeHexColor", () => {
  it("6 位十六进制统一为小写 #rrggbb", () => {
    expect(normalizeHexColor("#FFAA00")).toBe("#ffaa00");
    expect(normalizeHexColor("#fFaA00")).toBe("#ffaa00");
  });

  it("3 位简写扩展为 6 位", () => {
    expect(normalizeHexColor("#fa0")).toBe("#ffaa00");
    expect(normalizeHexColor("#ABC")).toBe("#aabbcc");
  });

  it("容忍首尾空白", () => {
    expect(normalizeHexColor("  #ffaa00\t")).toBe("#ffaa00");
  });

  it("非法格式返回空字符串", () => {
    expect(normalizeHexColor("ffaa00")).toBe(""); // 缺少 #
    expect(normalizeHexColor("#fff0")).toBe(""); // 4 位
    expect(normalizeHexColor("#12345")).toBe(""); // 5 位
    expect(normalizeHexColor("#1234567")).toBe(""); // 7 位
    expect(normalizeHexColor("#gggggg")).toBe(""); // 非十六进制字符
    expect(normalizeHexColor("")).toBe("");
  });

  it("非字符串输入返回空字符串", () => {
    expect(normalizeHexColor(null)).toBe("");
    expect(normalizeHexColor(undefined)).toBe("");
    expect(normalizeHexColor(0xffaa00)).toBe("");
    expect(normalizeHexColor({})).toBe("");
    expect(normalizeHexColor(["#ffaa00"])).toBe("");
  });
});

describe("buildPrimaryThemeColors", () => {
  it("中间调主色：hover 提亮、pressed 压暗，浅色主题 suppl 取 hover", () => {
    const colors = buildPrimaryThemeColors("#336699", false);
    expect(colors.primaryColor).toBe("#336699");
    expect(colors.primaryColorHover).toBe("#547ea9");
    expect(colors.primaryColorPressed).toBe("#29527a");
    expect(colors.primaryColorSuppl).toBe(colors.primaryColorHover);
  });

  it("深色主题 suppl 取主色本身", () => {
    const colors = buildPrimaryThemeColors("#336699", true);
    expect(colors.primaryColorSuppl).toBe("#336699");
  });

  it("过亮的颜色 hover 反向压暗，避免与主色无法区分", () => {
    const colors = buildPrimaryThemeColors("#ffffff", false);
    expect(colors.primaryColorHover).toBe("#e0e0e0");
    expect(colors.primaryColorPressed).toBe("#cccccc");
  });

  it("过暗的颜色 pressed 反向提亮", () => {
    const colors = buildPrimaryThemeColors("#000000", false);
    expect(colors.primaryColorHover).toBe("#292929");
    expect(colors.primaryColorPressed).toBe("#1f1f1f");
  });

  it("支持 3 位简写作为主色输入", () => {
    expect(buildPrimaryThemeColors("#369", false).primaryColor).toBe("#336699");
  });

  it("非法主色不抛异常", () => {
    expect(() => buildPrimaryThemeColors("not-a-color", false)).not.toThrow();
  });
});
