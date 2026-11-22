<script lang="ts" setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import type { GridTransferResult } from "grid-layout-plus";
import { getCardPlugin } from "@/cards/registry.ts";
import type { CardGroup, CardPlugin, PlatformCardRequired } from "@/cards/types.ts";
import { CARD_GROUP_TRANSFER_GROUP } from "@/config/default.ts";
import { useHomeCards } from "@/composables/useHomeCards.ts";
import { useHomeGroups } from "@/composables/useHomeGroups.ts";
import CardSelector from "@/components/CardSelector.vue";
import HomeGroupSection from "@/components/home/HomeGroupSection.vue";
import HomeEditMenu from "@/components/home/HomeEditMenu.vue";
import HomeEditTip from "@/components/home/HomeEditTip.vue";
import HomeBlankMenu from "@/components/home/HomeBlankMenu.vue";
import HomeCardMenu from "@/components/home/HomeCardMenu.vue";
import {
  disableSimulateMobilePlatform,
  enableSimulateMobilePlatform,
  platform,
} from "@/config/platform.ts";
import { isAuthenticated, logoutSession } from "@/stores/auth.ts";
import { demoMode } from "@/stores/demo.ts";

/** 卡片 CRUD 编排，数据访问与保存/回滚见 useHomeCards */
const {
  cards,
  platformCards,
  load,
  reload,
  handleCardMoved,
  handleCardResized,
  handleCardDuplicate,
  handleCardTransfer,
  addCard,
  handleCardMenuSelect,
} = useHomeCards();

const {
  sortedGroups,
  load: loadGroups,
  addGroup,
  renameGroup,
  toggleCollapsed,
  moveGroup,
  removeGroup,
} = useHomeGroups();

/** 首次加载完成前不展示空状态，避免请求途中闪一下 */
const ready = ref(false);

const enableGridEditMode = ref(false);
const simulateMobile = ref(false);
const showCardSelector = ref(false);
const selectedCardId = ref<string | null>(null);

const selectedCard = computed(
  () => cards.value.find((card) => card.id === selectedCardId.value) ?? null,
);

/** 添加卡片的目标分组，空白处右键时记录所在分组 */
const pendingGroupUuid = ref("");

/** 移动端浏览时过滤隐藏卡片；编辑态保留 */
const displayedCards = computed(() =>
  platform.value === "mobile" && !enableGridEditMode.value
    ? platformCards.value.filter((card) => card.mobileVisible)
    : platformCards.value,
);

const cardsByGroup = computed(() => {
  const map = new Map<CardGroup["id"], PlatformCardRequired[]>();
  for (const card of displayedCards.value) {
    const groupCards = map.get(card.groupUuid);
    if (groupCards) groupCards.push(card);
    else map.set(card.groupUuid, [card]);
  }
  return map;
});

const hasAnyCard = computed(() => cards.value.length > 0);

const emptyDescription = computed(() =>
  hasAnyCard.value ? "当前设备暂无可显示的卡片。" : "暂无任何卡片，请先添加。",
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
  pendingGroupUuid.value =
    (event.target as HTMLElement | null)?.closest<HTMLElement>("[data-group-id]")?.dataset
      .groupId ?? "";
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
    case "add-group":
      await addGroup();
      break;
    case "toggle-edit":
      await toggleGridEditMode();
      break;
  }
}

/** 顶部菜单添加卡片不继承空白处右键选中的分组 */
async function handleTopMenuSelect(key: string) {
  if (key === "add") {
    pendingGroupUuid.value = "";
  }
  await handleMenuSelect(key);
}

function handleCardSelect(cardPlugin: CardPlugin) {
  addCard(cardPlugin, pendingGroupUuid.value || sortedGroups.value[0]?.id || "");
}

function handleContextMenu(event: MouseEvent, item: PlatformCardRequired) {
  // 未登录不展示右键菜单
  if (!isAuthenticated.value) return;
  blankMenu.value?.close();
  cardMenu.value?.open(event, item, cards.value);
}

async function handleTransfer(result: GridTransferResult, groupId: string) {
  await handleCardTransfer(String(result.item.i), groupId, result.item.x, result.item.y);
}

/** 访客与演示站无法写回，折叠只保留在本地 */
function handleToggleCollapse(group: CardGroup) {
  if (isAuthenticated.value && !demoMode.value) {
    void toggleCollapsed(group);
    return;
  }
  group.collapsed = !group.collapsed;
}

async function handleRemoveGroup(group: CardGroup) {
  if (!(await removeGroup(group))) return;

  if (pendingGroupUuid.value === group.id) pendingGroupUuid.value = "";
  await reload();
}

function handleSelectCard(i: string) {
  selectedCardId.value = i;
}

function handlePageClick() {
  if (enableGridEditMode.value) selectedCardId.value = null;
}

function handleDeleteKey(event: KeyboardEvent) {
  if (!enableGridEditMode.value || event.key !== "Delete") return;

  const card = selectedCard.value;
  const target = event.target;
  if (!card) return;
  if (
    target instanceof HTMLElement &&
    target.closest("input, textarea, select, [contenteditable='true']")
  ) {
    return;
  }

  event.preventDefault();
  selectedCardId.value = null;
  void handleCardMenuSelect("delete", card);
}

watch(
  enableGridEditMode,
  (editing) => {
    document.documentElement.classList.toggle(EDITING_ROOT_CLASS, editing);
    window.removeEventListener("keydown", handleDeleteKey);

    if (editing) {
      window.addEventListener("keydown", handleDeleteKey);
    } else {
      selectedCardId.value = null;
    }
  },
  { immediate: true },
);

onBeforeUnmount(() => {
  document.documentElement.classList.remove(EDITING_ROOT_CLASS);
  window.removeEventListener("keydown", handleDeleteKey);
});

onMounted(async () => {
  await Promise.all([load(), loadGroups()]);
  ready.value = true;
});

// 登录态变化会改变接口下发范围（仅登录可见的卡片），需重新拉取
watch(isAuthenticated, () => {
  load();
});
</script>

<template>
  <div class="home-page" @click="handlePageClick" @contextmenu="handleBlankContextMenu">
    <!-- 首页公开访问，编辑入口仅对已登录用户展示 -->
    <HomeEditMenu
      v-if="isAuthenticated"
      :editing="enableGridEditMode"
      @select="handleTopMenuSelect"
    />

    <Transition name="edit-tip">
      <HomeEditTip v-if="enableGridEditMode" />
    </Transition>

    <CardSelector v-model:show="showCardSelector" @select="handleCardSelect" />

    <!-- 卡片超出可视区域时用 Naive UI 的浮层滚动条，避免原生滚动条挤压栅格宽度 -->
    <n-scrollbar>
      <div v-if="ready && displayedCards.length === 0" class="home-empty">
        <n-empty :description="emptyDescription">
          <template #extra>
            <n-flex justify="center">
              <n-button
                v-if="isAuthenticated && !hasAnyCard"
                type="primary"
                @click="handleTopMenuSelect('add')"
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

      <div v-if="sortedGroups.length" class="home-groups">
        <HomeGroupSection
          v-for="(group, index) in sortedGroups"
          :key="group.id"
          :cards="cardsByGroup.get(group.id) ?? []"
          :can-delete="sortedGroups.length > 1"
          :edit-mode="enableGridEditMode"
          :first="index === 0"
          :group="group"
          :last="index === sortedGroups.length - 1"
          :selected-id="selectedCardId"
          :simulate-mobile="simulateMobile"
          :transfer-group="CARD_GROUP_TRANSFER_GROUP"
          @contextmenu="handleContextMenu"
          @duplicate="handleCardDuplicate"
          @move="moveGroup"
          @moved="handleCardMoved"
          @remove="handleRemoveGroup"
          @rename="renameGroup"
          @resized="handleCardResized"
          @select="handleSelectCard"
          @toggle-collapse="handleToggleCollapse"
          @transfer="(result) => handleTransfer(result, group.id)"
        >
          <template #default="{ card }">
            <component :is="getCardPlugin(card.type)?.display" v-bind="card.config" />
          </template>
        </HomeGroupSection>
      </div>
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

.home-groups {
  display: flex;
  flex-direction: column;
  gap: 32px;
  width: 100%;
}

.edit-tip-enter-active,
.edit-tip-leave-active {
  transition:
    opacity 0.2s ease,
    transform 0.2s ease;
}

.edit-tip-enter-from,
.edit-tip-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}
</style>
