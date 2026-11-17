<script lang="ts" setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { getCardPlugin } from "@/cards/registry.ts";
import type { PlatformCardRequired } from "@/cards/types.ts";
import { useHomeCards } from "@/composables/useHomeCards.ts";
import CardSelector from "@/components/CardSelector.vue";
import HomeGrid from "../components/home/HomeGrid.vue";
import HomeEditMenu from "@/components/home/HomeEditMenu.vue";
import HomeBlankMenu from "@/components/home/HomeBlankMenu.vue";
import HomeCardMenu from "@/components/home/HomeCardMenu.vue";
import {
  disableSimulateMobilePlatform,
  enableSimulateMobilePlatform,
  platform,
} from "@/config/platform.ts";
import { isAuthenticated, logoutSession } from "@/stores/auth.ts";

/** 卡片 CRUD 编排，数据访问与保存/回滚见 useHomeCards */
const {
  cards,
  platformCards,
  load,
  reload,
  handleCardMoved,
  handleCardResized,
  handleCardDuplicate,
  addCard,
  handleCardMenuSelect,
} = useHomeCards();

/** 首次加载完成前不展示空状态，避免请求途中闪一下 */
const ready = ref(false);

const enableGridEditMode = ref(false);
const simulateMobile = ref(false);
const showCardSelector = ref(false);

/** 移动端浏览时过滤隐藏卡片；编辑态保留 */
const displayedCards = computed(() =>
  platform.value === "mobile" && !enableGridEditMode.value
    ? platformCards.value.filter((card) => card.mobileVisible)
    : platformCards.value,
);

const emptyDescription = computed(() =>
  cards.value.length === 0 ? "暂无任何卡片，请先添加。" : "当前设备暂无可显示的卡片。",
);

/** 卡片与空白区右键菜单在页面级互斥展示 */
const cardMenu = ref<InstanceType<typeof HomeCardMenu> | null>(null);
const blankMenu = ref<InstanceType<typeof HomeBlankMenu> | null>(null);

const router = useRouter();

/** 编辑模式挂在根元素上，用于禁用浏览器手势 */
const EDITING_ROOT_CLASS = "grid-editing";

async function toggleGridEditMode() {
  if (!enableGridEditMode.value) {
    enableGridEditMode.value = true;
    return;
  }

  simulateMobile.value = false;
  disableSimulateMobilePlatform();
  // 退出编辑时重新拉取，获取后端实际状态
  await reload();
  enableGridEditMode.value = false;
}

function handleBlankContextMenu(event: MouseEvent) {
  // grid-item 上的右键已在 HomeGrid 内阻止冒泡，这里只处理空白区域
  // 菜单操作均需登录
  if (!isAuthenticated.value) return;

  event.preventDefault();
  cardMenu.value?.close();
  blankMenu.value?.open(event);
}

async function handleMenuSelect(key: string) {
  switch (key) {
    case "logout":
      await logoutSession();
      await router.push("/login");
      break;
    case "mobile":
      if (platform.value === "desktop") {
        simulateMobile.value = true;
        enableSimulateMobilePlatform();
      }
      break;
    case "desktop":
      simulateMobile.value = false;
      disableSimulateMobilePlatform();
      break;
    case "add":
      showCardSelector.value = true;
      break;
    case "toggle-edit":
      await toggleGridEditMode();
      break;
  }
}

function handleContextMenu(event: MouseEvent, item: PlatformCardRequired) {
  // 未登录不展示右键菜单
  if (!isAuthenticated.value) return;
  blankMenu.value?.close();
  cardMenu.value?.open(event, item, cards.value);
}

watch(
  enableGridEditMode,
  (editing) => {
    document.documentElement.classList.toggle(EDITING_ROOT_CLASS, editing);
  },
  { immediate: true },
);

onBeforeUnmount(() => {
  document.documentElement.classList.remove(EDITING_ROOT_CLASS);
});

onMounted(async () => {
  await load();
  ready.value = true;
});

// 登录态变化会改变接口下发范围（仅登录可见的卡片），需重新拉取
watch(isAuthenticated, () => {
  load();
});
</script>

<template>
  <div class="home-page" @contextmenu="handleBlankContextMenu">
    <!-- 首页公开访问，编辑入口仅对已登录用户展示 -->
    <HomeEditMenu v-if="isAuthenticated" :editing="enableGridEditMode" @select="handleMenuSelect" />

    <CardSelector v-model:show="showCardSelector" @select="addCard" />

    <!-- 卡片超出可视区域时用 Naive UI 的浮层滚动条，避免原生滚动条挤压栅格宽度 -->
    <n-scrollbar>
      <div v-if="ready && displayedCards.length === 0" class="home-empty">
        <n-empty :description="emptyDescription">
          <template #extra>
            <n-flex justify="center">
              <n-button
                v-if="isAuthenticated && cards.length === 0"
                type="primary"
                @click="showCardSelector = true"
              >
                添加卡片
              </n-button>
              <n-button
                v-else-if="!isAuthenticated"
                color="orange"
                quaternary
                @click="router.push('/login')"
              >
                登录后可编辑布局
              </n-button>
            </n-flex>
          </template>
        </n-empty>
      </div>
      <HomeGrid
        :cards="displayedCards"
        :edit-mode="enableGridEditMode"
        :simulate-mobile="simulateMobile"
        @contextmenu="handleContextMenu"
        @duplicate="handleCardDuplicate"
        @moved="handleCardMoved"
        @resized="handleCardResized"
      >
        <template #default="{ card }">
          <component :is="getCardPlugin(card.type)?.display" v-bind="card.config" />
        </template>
      </HomeGrid>
    </n-scrollbar>

    <HomeCardMenu ref="cardMenu" @select="handleCardMenuSelect" />

    <HomeBlankMenu ref="blankMenu" :editing="enableGridEditMode" @select="handleMenuSelect" />
  </div>
</template>

<style scoped>
.home-page {
  --home-padding-max: 120px;
  position: relative;
  width: 100%;
  height: 100%;
  padding-top: 24px;
  display: flex;
  flex-direction: column;
  align-items: center;
  box-sizing: border-box;
  user-select: none;
}

/* 滚动区域撑满剩余高度；滚动条为浮层，不占用栅格宽度 */
/* 类名无法透传到 Naive UI 组件根节点，故直接匹配其内部滚动容器 */
.home-page > :deep(.n-scrollbar) {
  flex: 1;
  width: 100%;
  min-height: 0;
}

.home-page :deep(.n-scrollbar-content) {
  display: flex;
  justify-content: center;
}

/* 空状态不拦截事件，保证空白处右键仍可用 */
.home-empty {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
}

.home-empty :deep(.n-empty) {
  pointer-events: auto;
}
</style>
