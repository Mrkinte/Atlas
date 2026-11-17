<script lang="ts" setup>
import { computed, type CSSProperties } from "vue";
import { useNow } from "@/composables/useNow.ts";
import type { CountdownCardConfig } from "@/cards/CountdownCard/config.ts";
import {
  diffCalendarDays,
  formatDisplayDate,
  resolveTargetDate,
} from "@/cards/CountdownCard/date.ts";

/** 历史数据可能缺字段，全部可选并由默认值补齐 */
const props = withDefaults(defineProps<Partial<CountdownCardConfig>>(), {
  title: "",
  date: "",
  repeatYearly: false,
  backgroundColor: "",
  backgroundImage: "",
});

const now = useNow();

/** 实际目标日期：每年重复时取下一次发生日期 */
const targetDate = computed(() => resolveTargetDate(props.date, props.repeatYearly, now.value));

/** 距目标日期的自然日差值，0 表示今天 */
const days = computed(() => (targetDate.value ? diffCalendarDays(targetDate.value, now.value) : 0));

const displayDate = computed(() => (targetDate.value ? formatDisplayDate(targetDate.value) : ""));

const title = computed(() => props.title.trim() || "倒计时");

/** 背景色与背景图均可留空，留空时保持透明 */
const cardStyle = computed<CSSProperties>(() => {
  const style: CSSProperties = {};

  const color = props.backgroundColor.trim();
  if (color) style.backgroundColor = color;

  const image = props.backgroundImage.trim();
  if (image) {
    style.backgroundImage = `url(${JSON.stringify(image)})`;
    style.backgroundSize = "cover";
    style.backgroundPosition = "center";
    style.backgroundRepeat = "no-repeat";
  }

  return style;
});
</script>

<template>
  <div :style="cardStyle" class="countdown">
    <div class="countdown__body">
      <div :title="title" class="countdown__title">{{ title }}</div>

      <template v-if="targetDate">
        <div class="countdown__main">
          <span v-if="days === 0" class="countdown__today">今天</span>
          <template v-else>
            <span class="countdown__value">{{ Math.abs(days) }}</span>
            <span class="countdown__unit">{{ days > 0 ? "天" : "天前" }}</span>
          </template>
        </div>
        <div class="countdown__date">{{ displayDate }}</div>
      </template>

      <template v-else>
        <div class="countdown__empty">未设置日期</div>
        <div class="countdown__hint">请在编辑中设置目标日期</div>
      </template>
    </div>
  </div>
</template>

<style scoped>
/* 数字用 JetBrains Mono（SIL OFL 1.1），中文回退系统字体 */
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

.countdown {
  container-type: size;
  box-sizing: border-box;
  width: 100%;
  height: 100%;
  min-height: 64px;
  padding: 8px;
  overflow: hidden;
  border-radius: var(--border-radius);
  font-family: "JetBrains Mono", ui-monospace, "SFMono-Regular", Consolas, monospace;
  user-select: none;
}

.countdown__body {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  width: 100%;
  height: 100%;
  text-align: center;
}

.countdown__title {
  max-width: 100%;
  overflow: hidden;
  font-size: min(0.8rem, 14cqb);
  letter-spacing: 0.08em;
  opacity: 0.65;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.countdown__main {
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 0.18em;
  font-size: min(2.6rem, 38cqi, 42cqb);
  line-height: 1;
  white-space: nowrap;
}

.countdown__value {
  font-size: 1em;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.02em;
}

.countdown__unit {
  font-size: 0.34em;
  opacity: 0.7;
}

.countdown__today {
  font-size: min(2.2rem, 30cqi, 36cqb);
  font-weight: 700;
  letter-spacing: 0.04em;
}

.countdown__date {
  font-size: min(0.72rem, 13cqb);
  letter-spacing: 0.04em;
  opacity: 0.55;
  white-space: nowrap;
}

.countdown__empty {
  font-size: min(1rem, 20cqb);
  opacity: 0.55;
}

.countdown__hint {
  overflow: hidden;
  font-size: 0.75rem;
  opacity: 0.4;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 单行高度（2×1）隐藏日期与提示，保证数字完整展示 */
@container (max-height: 72px) {
  .countdown__body {
    gap: 1px;
  }

  .countdown__title {
    font-size: min(0.68rem, 24cqb);
  }

  .countdown__main {
    font-size: min(2.6rem, 60cqi, 62cqb);
  }

  .countdown__today {
    font-size: min(2.2rem, 46cqi, 52cqb);
  }

  .countdown__date,
  .countdown__hint {
    display: none;
  }
}
</style>
