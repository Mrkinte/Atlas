import { computed, ref } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { useCards } from "@/composables/useCards.ts";
import { openEditor } from "@/composables/useCardEditorHost.ts";
import { getCardPlugin } from "@/cards/registry.ts";
import type { CardPlugin, CardRequired, PlatformCardRequired } from "@/cards/types.ts";
import { findAppendPosition, findNextPosition } from "@/utils/cardLayoutCalculate.ts";
import { MOBILE_GRID_COLS } from "@/config/default.ts";
import { platform } from "@/config/platform.ts";
import { settings } from "@/stores/settings.ts";
import { v4 as uuidv4 } from "uuid";

/**
 * 卡片 CRUD 编排：改哪张卡片、何时持久化、失败如何回滚
 * 数据访问与编辑器挂载分别委托 useCards / useCardEditorHost
 */
export function useHomeCards() {
  const { cards, load, create, update, remove } = useCards();
  const message = useAppMessage();

  const syncing = ref(false);

  const platformCards = computed<PlatformCardRequired[]>(() =>
    cards.value.map((card) => {
      const layout = platform.value === "mobile" ? card.sm : card.lg;
      return {
        i: card.id,
        x: layout.x,
        y: layout.y,
        w: layout.width,
        h: layout.height,
        minW: card.minWidth,
        minH: card.minHeight,
        groupUuid: card.groupUuid,
        isVisible: card.isVisible,
        mobileVisible: card.mobileVisible,
        type: card.type,
        config: card.config,
      };
    }),
  );

  async function handleCardMoved(i: string, newX: number, newY: number) {
    const card = cards.value.find((it) => it.id === i);
    if (!card) return;
    const layout = platform.value === "mobile" ? card.sm : card.lg;
    layout.x = newX;
    layout.y = newY;
    const err = await update(card);
    if (err) {
      message.error(err);
    }
  }

  async function handleCardResized(i: string, newH: number, newW: number) {
    const card = cards.value.find((it) => it.id === i);
    if (!card) return;
    const layout = platform.value === "mobile" ? card.sm : card.lg;
    layout.width = newW;
    layout.height = newH;
    const err = await update(card);
    if (err) {
      message.error(err);
    }
  }

  async function handleCardDuplicate(card: PlatformCardRequired, x: number, y: number) {
    const source = cards.value.find((it) => it.id === card.i);
    if (!source) return;

    const groupUuid = card.groupUuid;
    const groupCards = cards.value.filter((it) => it.groupUuid === groupUuid);
    // 非当前平台无法从拖拽得到坐标，追加到该分组现有卡片下方
    const { smY, lgY } = findNextPosition(groupCards);
    const isMobile = platform.value === "mobile";

    const newCard: CardRequired = {
      id: uuidv4(),
      groupUuid,
      sm: isMobile
        ? { x, y, width: card.w, height: card.h }
        : { x: 0, y: smY, width: source.sm.width, height: source.sm.height },
      lg: isMobile
        ? { x: 0, y: lgY, width: source.lg.width, height: source.lg.height }
        : { x, y, width: card.w, height: card.h },
      minWidth: source.minWidth,
      minHeight: source.minHeight,
      isVisible: source.isVisible,
      mobileVisible: source.mobileVisible,
      type: source.type,
      // config 是响应式代理，结构化克隆会失败，走 JSON 拷贝
      config: JSON.parse(JSON.stringify(source.config)) as Record<string, unknown>,
    };

    syncing.value = true;
    await create(newCard);
    syncing.value = false;
    cards.value.push(newCard);
  }

  async function handleCardTransfer(cardId: string, targetGroupUuid: string, x: number, y: number) {
    const card = cards.value.find((it) => it.id === cardId);
    if (!card) return;

    const isMobile = platform.value === "mobile";
    const layout = isMobile ? card.sm : card.lg;
    // 源栅格与目标栅格可能各派发一次 transfer，第二次分组与坐标都没变
    if (card.groupUuid === targetGroupUuid && layout.x === x && layout.y === y) return;

    layout.x = x;
    layout.y = y;

    // 另一平台拿不到落点，追加到目标分组末尾避免重叠
    const otherKey = isMobile ? "lg" : "sm";
    const otherCards = cards.value.filter(
      (it) => it.groupUuid === targetGroupUuid && it.id !== card.id,
    );
    const otherLayout = card[otherKey];
    const other = findAppendPosition(
      otherCards,
      otherKey,
      otherLayout.width,
      otherLayout.height,
      isMobile ? settings.desktopCols : MOBILE_GRID_COLS,
    );
    otherLayout.x = other.x;
    otherLayout.y = other.y;

    card.groupUuid = targetGroupUuid;

    const err = await update(card);
    if (err) message.error(err);
  }

  async function addCard(cardPlugin: CardPlugin, groupUuid: string) {
    const { minWidth: width, minHeight: height } = cardPlugin;
    const groupCards = cards.value.filter((it) => it.groupUuid === groupUuid);
    const sm = findAppendPosition(groupCards, "sm", width, height, MOBILE_GRID_COLS);
    const lg = findAppendPosition(groupCards, "lg", width, height, settings.desktopCols);

    const newCard: CardRequired = {
      id: uuidv4(),
      groupUuid,
      sm: { ...sm, width, height },
      lg: { ...lg, width, height },
      minWidth: width,
      minHeight: height,
      isVisible: true,
      mobileVisible: true,
      type: cardPlugin.name,
      config: cardPlugin.defaultConfig,
    };
    syncing.value = true;
    await create(newCard);
    syncing.value = false;
    cards.value.push(newCard);
  }

  async function reload() {
    syncing.value = true;
    await load();
    syncing.value = false;
  }

  /** 保存时写回 config，失败抛错由编辑器提示 */
  function editCard(card: CardRequired) {
    openEditor(getCardPlugin(card.type)?.editor, {
      props: card.config,
      onSave: async (data) => {
        card.config = data;
        const err = await update(card);
        if (err) throw new Error(err);
      },
    });
  }

  /** 保存失败回滚开关状态 */
  async function toggleLoginOnly(card: CardRequired) {
    card.isVisible = !card.isVisible;
    const err = await update(card);
    if (err) {
      card.isVisible = !card.isVisible;
      message.error(err);
    } else {
      message.success(card.isVisible ? "已取消：所有人可见" : "已设为仅登录可见");
    }
  }

  /** 保存失败回滚开关状态 */
  async function toggleMobileVisible(card: CardRequired) {
    card.mobileVisible = !card.mobileVisible;
    const err = await update(card);
    if (err) {
      card.mobileVisible = !card.mobileVisible;
      message.error(err);
    } else {
      message.success(card.mobileVisible ? "已取消：移动端显示" : "已设为移动端隐藏");
    }
  }

  async function handleCardMenuSelect(key: string, card: CardRequired) {
    if (key === "edit") {
      editCard(card);
    } else if (key === "login-only") {
      await toggleLoginOnly(card);
    } else if (key === "mobile-hidden") {
      await toggleMobileVisible(card);
    } else if (key === "delete") {
      await remove(card.id);
    }
  }

  return {
    cards,
    syncing,
    platformCards,
    load,
    reload,
    handleCardMoved,
    handleCardResized,
    handleCardDuplicate,
    handleCardTransfer,
    addCard,
    handleCardMenuSelect,
  };
}
