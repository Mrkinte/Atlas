<script lang="ts" setup>
import { computed, type CSSProperties, ref } from "vue";
import type { CarouselInst } from "naive-ui";
import ChevronLeft24Filled from "@vicons/fluent/es/ChevronLeft24Filled.js";
import ChevronRight24Filled from "@vicons/fluent/es/ChevronRight24Filled.js";
import ImageMultiple24Regular from "@vicons/fluent/es/ImageMultiple24Regular.js";
import type { SlideCardConfig, SlideItem } from "@/cards/SlideCard/config.ts";

/** 历史数据可能缺字段，全部可选并由默认值补齐 */
const props = withDefaults(defineProps<Partial<SlideCardConfig>>(), {
  slides: () => [],
  autoplay: true,
  interval: 5,
  objectFit: "cover",
  showTitle: true,
  showDots: true,
  showArrow: true,
  titleColor: "",
});

/** 历史数据可能残留空行，过滤掉没有地址的项 */
const slides = computed<SlideItem[]>(() =>
  (props.slides ?? [])
    .filter((item) => Boolean(item?.url))
    .map((item) => ({ url: item.url, title: item.title ?? "" })),
);

const hasMultiple = computed(() => slides.value.length > 1);

/** 只有一张图片时不轮播，避免无意义的切换动画 */
const autoplayEnabled = computed(() => props.autoplay && hasMultiple.value);
const arrowVisible = computed(() => props.showArrow && hasMultiple.value);

/** 间隔按秒存储，组件需要毫秒 */
const intervalMs = computed(() => Math.max(1, Number(props.interval) || 1) * 1000);

/** 轮播平移过渡时长，与 n-carousel 的 transition-style 保持同一个值 */
const SLIDE_TRANSITION_MS = 300;

/** 自动轮播时缓慢放大 */
const zoomEnabled = computed(() => autoplayEnabled.value && props.objectFit === "cover");

/** 扣掉平移过渡时间，让放大结束与下一次切换衔接 */
const zoomDurationMs = computed(() =>
  Math.max(SLIDE_TRANSITION_MS, intervalMs.value - SLIDE_TRANSITION_MS),
);

/** 与上面的时长计算保持一致 */
const slideTransitionStyle = {
  transitionDuration: `${SLIDE_TRANSITION_MS}ms`,
};

/** 标题颜色为空时由样式里的默认白色兜底 */
const rootStyle = computed<CSSProperties>(() => {
  const style: Record<string, string> = {
    "--slide-zoom-duration": `${zoomDurationMs.value}ms`,
  };
  if (props.titleColor) style["--slide-title-color"] = props.titleColor;
  return style as CSSProperties;
});

/** 自定义箭头，用实例方法切换 */
const carouselRef = ref<CarouselInst | null>(null);
</script>

<template>
  <div :class="{ 'slide--zoom': zoomEnabled }" :style="rootStyle" class="slide">
    <!-- 没有图片时的占位，避免卡片塌陷成空白 -->
    <div v-if="!slides.length" class="slide__placeholder">
      <n-icon :size="24">
        <ImageMultiple24Regular />
      </n-icon>
      <span class="slide__placeholder-text">暂无图片</span>
      <span class="slide__placeholder-hint">在卡片编辑中添加图片</span>
    </div>

    <template v-else>
      <n-carousel
        ref="carouselRef"
        :autoplay="autoplayEnabled"
        :interval="intervalMs"
        :loop="true"
        :show-arrow="false"
        :show-dots="props.showDots && hasMultiple"
        :slides-per-view="1"
        :transition-style="slideTransitionStyle"
        class="slide__carousel"
        dot-placement="bottom"
        dot-type="line"
        draggable
      >
        <div v-for="(item, index) in slides" :key="`${index}-${item.url}`" class="slide__item">
          <img
            :alt="item.title || `第 ${index + 1} 张图片`"
            :class="`slide__image--${props.objectFit}`"
            :src="item.url"
            class="slide__image"
            draggable="false"
            @transitionend.stop
          />

          <template v-if="props.showTitle && item.title">
            <div class="slide__scrim" />
            <div class="slide__caption">
              <span class="slide__title">{{ item.title }}</span>
            </div>
          </template>
        </div>
      </n-carousel>

      <template v-if="arrowVisible">
        <div class="slide__arrow slide__arrow--prev" @click.stop="carouselRef?.prev()">
          <n-icon :size="16">
            <ChevronLeft24Filled />
          </n-icon>
        </div>
        <div class="slide__arrow slide__arrow--next" @click.stop="carouselRef?.next()">
          <n-icon :size="16">
            <ChevronRight24Filled />
          </n-icon>
        </div>
      </template>
    </template>
  </div>
</template>

<style scoped>
.slide {
  /* 按卡片实际宽高缩放字号，需要尺寸包含才能使用 cqh */
  container-type: size;
  /* 脚本按轮播间隔写入，兜底值 */
  --slide-zoom-duration: 5s;
  /* 放大倍数，再大容易出现明显的裁剪感 */
  --slide-zoom-scale: 1.06;
  position: relative;
  box-sizing: border-box;
  width: 100%;
  height: 100%;
  /* 卡片选择器预览处高度自适应，用最小高度兜底 */
  min-height: 120px;
  overflow: hidden;
  border-radius: var(--border-radius);
  background-color: rgba(127, 127, 127, 0.08);
  user-select: none;
}

.slide__placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2cqh;
  width: 100%;
  height: 100%;
  min-height: 120px;
  opacity: 0.42;
}

.slide__placeholder-text {
  font-size: clamp(11px, 6cqw, 15px);
  line-height: 1.2;
}

.slide__placeholder-hint {
  font-size: clamp(10px, 4.4cqw, 12px);
  line-height: 1.2;
  opacity: 0.8;
}

.slide__carousel {
  width: 100%;
  height: 100%;
}

.slide__item {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
  background-color: rgba(127, 127, 127, 0.08);
}

.slide__image {
  display: block;
  width: 100%;
  height: 100%;
  transform: scale(1);
  /* 离开放大状态回到原始比例时的过渡 */
  transition: transform 0.36s ease-out;
}

.slide__image--cover {
  object-fit: cover;
}

.slide__image--contain {
  object-fit: contain;
}

/* 当前幻灯片在停留期间匀速放大 */
.slide--zoom :deep(.n-carousel__slide--current) .slide__image--cover {
  animation: slide-zoom var(--slide-zoom-duration) linear forwards;
}

@keyframes slide-zoom {
  from {
    transform: scale(1);
  }
  to {
    transform: scale(var(--slide-zoom-scale));
  }
}

/* 底部渐变遮罩，保证浅色图片上的标题也可读 */
.slide__scrim {
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  height: 62%;
  background: linear-gradient(
    to top,
    rgba(0, 0, 0, 0.72) 0%,
    rgba(0, 0, 0, 0.28) 45%,
    transparent 100%
  );
  pointer-events: none;
}

.slide__caption {
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  /* 底部留出指示点的位置，避免标题与圆点重叠 */
  padding: min(3cqw, 10px) min(5cqw, 18px) min(11cqw, 26px);
  pointer-events: none;
}

.slide__title {
  display: block;
  overflow: hidden;
  color: var(--slide-title-color, #fff);
  font-size: clamp(13px, min(7cqw, 21cqh), 30px);
  font-weight: 700;
  line-height: 1.3;
  letter-spacing: 0.02em;
  white-space: nowrap;
  text-overflow: ellipsis;
  text-shadow: 0 2px 10px rgba(0, 0, 0, 0.55);
}

.slide__arrow {
  position: absolute;
  top: 50%;
  z-index: 2;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  color: #fff;
  background-color: rgba(0, 0, 0, 0.32);
  backdrop-filter: blur(8px);
  cursor: pointer;
  opacity: 0.5;
  transform: translateY(-50%);
  transition:
    opacity 0.25s ease,
    background-color 0.25s ease;
}

.slide__arrow:hover {
  opacity: 1;
  background-color: rgba(0, 0, 0, 0.55);
}

.slide__arrow--prev {
  left: 10px;
}

.slide__arrow--next {
  right: 10px;
}
</style>
