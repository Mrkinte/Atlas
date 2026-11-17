<script lang="ts" setup>
import { computed, type CSSProperties } from "vue";
import { useNow } from "@/composables/useNow.ts";
import {
  formatAsciiDate,
  formatChineseDate,
  formatHour,
  formatMinute,
  formatPeriod,
  formatSecond,
} from "@/cards/ClockCard/clockTime.ts";
import type { ClockCardConfig } from "@/cards/ClockCard/config.ts";

/** 历史数据可能缺字段，全部可选并由默认值补齐 */
const props = withDefaults(defineProps<Partial<ClockCardConfig>>(), {
  variant: "pixel",
  showSeconds: false,
  showDate: true,
  hour24: true,
  accentColor: "",
});

const now = useNow();

const hours = computed(() => formatHour(now.value, props.hour24));
const minutes = computed(() => formatMinute(now.value));
const seconds = computed(() => formatSecond(now.value));
const period = computed(() => formatPeriod(now.value));

/** 像素与简约字体不含中文字形，使用 ASCII 日期；手写风用中文日期 */
const dateText = computed(() =>
  props.variant === "handwritten" ? formatChineseDate(now.value) : formatAsciiDate(now.value),
);

/** 文字颜色为空时跟随卡片文字颜色 */
const clockStyle = computed<CSSProperties>(() =>
  props.accentColor ? { "--clock-accent": props.accentColor } : {},
);
</script>

<template>
  <div
    :class="[`clock--${props.variant}`, { 'clock--seconds': props.showSeconds }]"
    :style="clockStyle"
    class="clock"
  >
    <div class="clock__time">
      <span class="clock__value">{{ hours }}</span>
      <span class="clock__colon">:</span>
      <span class="clock__value">{{ minutes }}</span>
      <template v-if="props.showSeconds">
        <span class="clock__colon">:</span>
        <span class="clock__value">{{ seconds }}</span>
      </template>
      <span v-if="!props.hour24" class="clock__period">{{ period }}</span>
    </div>

    <div v-if="props.showDate" class="clock__date">{{ dateText }}</div>
  </div>
</template>

<style scoped>
/* Press Start 2P（SIL OFL 1.1），仅含拉丁字形 */
@font-face {
  font-family: "Press Start 2P";
  src: url("../../assets/fonts/press-start-2p.woff2") format("woff2");
  font-display: swap;
}

/* DaMengLuoShenFu，含中文字形 */
@font-face {
  font-family: "DaMengLuoShenFu";
  src: url("../../assets/fonts/DaMengLuoShenFu-Regular-2.woff2") format("woff2");
  font-display: swap;
  font-weight: 400;
}

/* JetBrains Mono（SIL OFL 1.1），仅含拉丁字形 */
@font-face {
  font-family: "JetBrains Mono";
  src: url("../../assets/fonts/jetbrains-mono-regular.woff2") format("woff2");
  font-display: swap;
  font-weight: 400;
}

@font-face {
  font-family: "JetBrains Mono";
  src: url("../../assets/fonts/jetbrains-mono-bold.woff2") format("woff2");
  font-display: swap;
  font-weight: 700;
}

.clock {
  /* 按卡片实际宽高的较小值缩放字号，需要尺寸包含才能使用 cqh */
  container-type: size;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  /* 卡片选择器预览处高度自适应，用最小高度兜底 */
  min-height: 64px;
  overflow: hidden;
  user-select: none;
  /* 默认跟随卡片文字颜色，设置了文字颜色时由 --clock-accent 覆盖 */
  color: var(--clock-accent, currentColor);
}

.clock__time {
  display: flex;
  align-items: baseline;
  justify-content: center;
  line-height: 1;
  white-space: nowrap;
}

/* 数字等宽，避免秒数跳动时整行位移 */
.clock__value,
.clock__colon {
  font-variant-numeric: tabular-nums;
}

.clock__period {
  font-size: 0.42em;
  margin-left: 0.6em;
  letter-spacing: 0.08em;
}

.clock--pixel {
  padding: 6px 10px;
  font-family: "Press Start 2P", ui-monospace, "Courier New", monospace;
}

.clock--pixel .clock__time {
  font-size: min(17cqw, 42cqh);
}

/* 带秒时字符数变多，需要按比例缩小 */
.clock--pixel.clock--seconds .clock__time {
  font-size: min(12cqw, 36cqh);
}

.clock--pixel .clock__colon {
  animation: clock-blink 1s step-end infinite;
}

.clock--pixel .clock__date {
  margin-top: min(3cqw, 6cqh);
  font-size: min(5.5cqw, 12cqh);
  letter-spacing: 0.2em;
  opacity: 0.8;
}

@keyframes clock-blink {
  50% {
    opacity: 0.2;
  }
}

.clock--handwritten {
  padding: 6px 12px;
  font-family: "DaMengLuoShenFu", "Kaiti SC", "STKaiti", "KaiTi", "楷体", cursive;
  transform: rotate(-0.7deg);
}

.clock--handwritten .clock__time {
  font-size: min(30cqw, 54cqh);
  font-weight: 400;
}

.clock--handwritten.clock--seconds .clock__time {
  font-size: min(28cqw, 46cqh);
}

.clock--handwritten .clock__date {
  margin-top: 2px;
  font-size: min(10cqw, 17cqh);
  letter-spacing: 0.04em;
  opacity: 0.82;
}

.clock--jetbrains {
  padding: 6px 12px;
  font-family: "JetBrains Mono", ui-monospace, "SFMono-Regular", Consolas, monospace;
  letter-spacing: -0.02em;
}

.clock--jetbrains .clock__time {
  font-size: min(26cqw, 50cqh);
  font-weight: 700;
}

.clock--jetbrains.clock--seconds .clock__time {
  font-size: min(18cqw, 42cqh);
}

.clock--jetbrains .clock__colon {
  opacity: 0.45;
}

.clock--jetbrains .clock__date {
  margin-top: min(3cqw, 6cqh);
  font-size: min(7.5cqw, 14cqh);
  letter-spacing: 0.12em;
  opacity: 0.62;
}
</style>
