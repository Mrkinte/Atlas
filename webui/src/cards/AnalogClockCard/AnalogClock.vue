<script lang="ts" setup>
import { computed, type CSSProperties } from "vue";
import { useNow } from "@/composables/useNow.ts";
import type { AnalogClockConfig } from "@/cards/AnalogClockCard/config.ts";

/** 历史数据可能缺字段，全部可选并由默认值补齐 */
const props = withDefaults(defineProps<Partial<AnalogClockConfig>>(), {
  showSeconds: true,
  smoothSeconds: false,
  showNumbers: false,
  accentColor: "",
  backgroundOpacity: 100,
});

/** 秒针连续扫动时需要按动画帧刷新时间 */
const now = useNow(() => props.showSeconds && props.smoothSeconds);

const hoursAngle = computed(() => (now.value.getHours() % 12) * 30 + now.value.getMinutes() * 0.5);
const minutesAngle = computed(() => now.value.getMinutes() * 6 + now.value.getSeconds() * 0.1);
const secondsAngle = computed(() => {
  const second = props.smoothSeconds
    ? now.value.getSeconds() + now.value.getMilliseconds() / 1000
    : now.value.getSeconds();
  return second * 6;
});

/** 整点刻度 */
const hourTicks = Array.from({ length: 12 }, (_, index) => index * 30);

/** 分钟刻度，跳过整点位置 */
const minuteTicks = Array.from({ length: 60 }, (_, index) => index * 6).filter(
  (angle) => angle % 30 !== 0,
);

/** 表盘数字坐标，12 在正上方 */
const numbers = Array.from({ length: 12 }, (_, index) => {
  const angle = ((index + 1) * 30 * Math.PI) / 180;
  return {
    value: index + 1,
    x: 50 + 33 * Math.sin(angle),
    y: 50 - 33 * Math.cos(angle),
  };
});

/** 背景不透明度归一化到 0-1，非法值回退为不透明 */
const backgroundOpacity = computed(() => {
  const value = Number(props.backgroundOpacity);
  return Number.isFinite(value) ? Math.min(100, Math.max(0, value)) / 100 : 1;
});

/** 强调色为空时由样式提供默认值 */
const clockStyle = computed<CSSProperties>(() => {
  const style: Record<string, string> = {
    "--clock-bg-opacity": String(backgroundOpacity.value),
  };
  if (props.accentColor) style["--clock-accent"] = props.accentColor;
  return style;
});
</script>

<template>
  <div :style="clockStyle" class="analog">
    <svg class="analog__face" role="img" viewBox="0 0 100 100">
      <circle class="analog__dial" cx="50" cy="50" r="47" />

      <line
        v-for="angle in minuteTicks"
        :key="`m-${angle}`"
        :transform="`rotate(${angle} 50 50)`"
        class="analog__tick analog__tick--minor"
        x1="50"
        x2="50"
        y1="6"
        y2="9"
      />
      <line
        v-for="angle in hourTicks"
        :key="`h-${angle}`"
        :transform="`rotate(${angle} 50 50)`"
        class="analog__tick analog__tick--major"
        x1="50"
        x2="50"
        y1="5"
        y2="11"
      />

      <text
        v-for="item in props.showNumbers ? numbers : []"
        :key="`n-${item.value}`"
        :x="item.x"
        :y="item.y"
        class="analog__number"
        dominant-baseline="central"
        text-anchor="middle"
      >
        {{ item.value }}
      </text>

      <line
        :transform="`rotate(${hoursAngle} 50 50)`"
        class="analog__hand analog__hand--hour"
        x1="50"
        x2="50"
        y1="56"
        y2="29"
      />
      <line
        :transform="`rotate(${minutesAngle} 50 50)`"
        class="analog__hand analog__hand--minute"
        x1="50"
        x2="50"
        y1="56"
        y2="18"
      />
      <g v-if="props.showSeconds" :transform="`rotate(${secondsAngle} 50 50)`">
        <line class="analog__hand analog__hand--second" x1="50" x2="50" y1="58" y2="12" />
        <circle class="analog__second-dot" cx="50" cy="58" r="1.8" />
      </g>

      <circle class="analog__cap" cx="50" cy="50" r="2.4" />
    </svg>
  </div>
</template>

<style scoped>
.analog {
  --clock-accent: #e2574c;
  /* 组件内联样式会覆盖，兜底为不透明 */
  --clock-bg-opacity: 1;
  /* 按卡片实际宽高等比缩放表盘，需要尺寸包含才能使用 cqh */
  container-type: size;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  /* 卡片选择器预览处高度自适应，用最小高度兜底 */
  min-height: 96px;
  user-select: none;
}

.analog__face {
  width: min(88cqw, 88cqh);
  height: min(88cqw, 88cqh);
}

.analog__dial {
  /* 卡片底色叠加极淡文字色作为表盘背景 */
  fill: color-mix(
    in srgb,
    color-mix(in srgb, currentColor 6%, var(--n-color, transparent))
      calc(var(--clock-bg-opacity) * 100%),
    transparent
  );
  stroke: color-mix(in srgb, currentColor 42%, transparent);
  stroke-width: 1.5;
}

.analog__tick {
  stroke: currentColor;
  stroke-linecap: round;
}

.analog__tick--minor {
  stroke-width: 0.8;
  opacity: 0.35;
}

.analog__tick--major {
  stroke-width: 2;
  opacity: 0.75;
}

.analog__number {
  fill: currentColor;
  font-size: 7.4px;
  opacity: 0.8;
}

.analog__hand {
  stroke-linecap: round;
}

.analog__hand--hour {
  stroke: currentColor;
  stroke-width: 3.2;
}

.analog__hand--minute {
  stroke: currentColor;
  stroke-width: 2;
}

.analog__hand--second {
  stroke: var(--clock-accent);
  stroke-width: 1;
}

.analog__second-dot {
  fill: var(--clock-accent);
}

.analog__cap {
  fill: currentColor;
}
</style>
