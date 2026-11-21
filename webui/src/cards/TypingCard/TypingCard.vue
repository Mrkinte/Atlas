<script lang="ts" setup>
import { computed, type CSSProperties, onMounted, onUnmounted, ref, watch } from "vue";
import type { TypingCardConfig, TypingCardFont } from "@/cards/TypingCard/config.ts";

/** 历史数据可能缺字段，全部可选并由默认值补齐 */
const props = withDefaults(defineProps<Partial<TypingCardConfig>>(), {
  text: "",
  color: "",
  fontSize: 32,
  fontFamily: "electrolize",
  typingSpeed: 100,
  loop: false,
  loopInterval: 2000,
});

/** default 交给卡片自身字体 */
const FONT_STACKS: Record<TypingCardFont, string | undefined> = {
  default: undefined,
  electrolize: '"Electrolize", sans-serif',
  dameng: '"DaMengLuoShenFu", "Kaiti SC", "STKaiti", "KaiTi", "楷体", cursive',
  jetbrains: '"JetBrains Mono", ui-monospace, "SFMono-Regular", Consolas, monospace',
  pixel: '"Press Start 2P", ui-monospace, "Courier New", monospace',
};

/** 开始打字前的停顿，毫秒 */
const START_DELAY = 400;

const typedText = ref("");
const isTyping = ref(false);

let typingTimer: ReturnType<typeof setTimeout> | undefined;

/** 文字或速度变化时重播 */
function startTyping() {
  clearTimeout(typingTimer);
  typedText.value = "";
  isTyping.value = true;

  const text = props.text;
  const speed = props.typingSpeed;

  const typeCharacter = (index: number) => {
    if (index < text.length) {
      typedText.value = text.slice(0, index + 1);
      typingTimer = setTimeout(() => typeCharacter(index + 1), speed);
    } else {
      isTyping.value = false;
      // 空文本循环会让光标反复闪动，直接不循环
      if (props.loop && text.length > 0) {
        typingTimer = setTimeout(startTyping, props.loopInterval);
      }
    }
  };

  typingTimer = setTimeout(() => typeCharacter(0), START_DELAY);
}

const cardStyle = computed<CSSProperties>(() => ({
  color: props.color || undefined,
  fontFamily: FONT_STACKS[props.fontFamily],
  fontSize: `${props.fontSize}px`,
}));

onMounted(startTyping);

watch(() => [props.text, props.typingSpeed, props.loop], startTyping);

onUnmounted(() => {
  clearTimeout(typingTimer);
});
</script>

<template>
  <div :class="{ 'typing-card--typing': isTyping }" :style="cardStyle" class="typing-card">
    <span class="typing-card__text">{{ typedText }}<span class="typing-card__cursor" /></span>
  </div>
</template>

<style scoped>
/* Electrolize（SIL OFL 1.1），仅含拉丁字形 */
@font-face {
  font-family: "Electrolize";
  src: url("../../assets/fonts/Electrolize-Regular.ttf") format("truetype");
  font-display: swap;
  font-weight: 400;
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

/* Press Start 2P（SIL OFL 1.1），仅含拉丁字形 */
@font-face {
  font-family: "Press Start 2P";
  src: url("../../assets/fonts/press-start-2p.woff2") format("woff2");
  font-display: swap;
}

.typing-card {
  box-sizing: border-box;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  /* 卡片选择器预览处高度自适应，用最小高度兜底 */
  min-height: 64px;
  padding: 8px;
  overflow: hidden;
  line-height: 1.25;
  user-select: none;
}

.typing-card__text {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  text-shadow: 0 0 8px currentColor;
}

.typing-card__cursor {
  display: inline-block;
  width: max(2px, 0.08em);
  height: 1.05em;
  margin-left: 0.08em;
  border-radius: 1px;
  background-color: currentColor;
  box-shadow: 0 0 8px currentColor;
  /* 光标比文字高出的部分上下均分 */
  vertical-align: -0.175em;
  animation: typing-card-blink 1s step-end infinite;
}

/* 打字过程中光标常亮，不再闪烁 */
.typing-card--typing .typing-card__cursor {
  animation: none;
}

@keyframes typing-card-blink {
  50% {
    opacity: 0;
  }
}
</style>
