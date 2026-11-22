import { ref } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { client } from "@/apis/client.ts";
import type { components } from "@/apis/schema";
import type { CardRequired } from "@/cards/types.ts";
import { NETWORK_ERROR, SETTINGS_SAVE_FAILED } from "@/config/messages.ts";
import { demoMode } from "@/stores/demo.ts";

type CardResponse = components["schemas"]["CardResponse"];
type CreateCardRequest = components["schemas"]["CreateCardRequest"];
type UpdateCardRequest = components["schemas"]["UpdateCardRequest"];

/** 演示模式只取一次快照，退出编辑时的重载不能覆盖本地改动 */
let loadedInDemo = false;

function toCardPayload(card: CardRequired): CreateCardRequest {
  return {
    uuid: card.id,
    groupUuid: card.groupUuid,
    smX: card.sm.x,
    smY: card.sm.y,
    smWidth: card.sm.width,
    smHeight: card.sm.height,
    lgX: card.lg.x,
    lgY: card.lg.y,
    lgWidth: card.lg.width,
    lgHeight: card.lg.height,
    minWidth: card.minWidth ?? 1,
    minHeight: card.minHeight ?? 1,
    isVisible: card.isVisible,
    mobileVisible: card.mobileVisible,
    type: card.type,
    data: card.config,
  };
}

export function useCards() {
  const message = useAppMessage();
  const cards = ref<CardRequired[]>([]);
  const loading = ref(false);

  async function load() {
    if (demoMode.value && loadedInDemo) return;

    loading.value = true;
    try {
      const { data, error } = await client.GET("/api/cards");
      if (error) {
        message.error("加载布局失败");
        return;
      }

      loadedInDemo = demoMode.value;
      cards.value = data.map((card: CardResponse): CardRequired => ({
        id: card.uuid,
        groupUuid: card.groupUuid,
        sm: {
          x: card.smX,
          y: card.smY,
          width: card.smWidth,
          height: card.smHeight,
        },
        lg: {
          x: card.lgX,
          y: card.lgY,
          width: card.lgWidth,
          height: card.lgHeight,
        },
        minWidth: card.minWidth,
        minHeight: card.minHeight,
        isVisible: card.isVisible,
        mobileVisible: card.mobileVisible,
        type: card.type,
        config: (card.data ?? {}) as Record<string, unknown>,
      }));
    } catch {
      message.error(NETWORK_ERROR);
    } finally {
      loading.value = false;
    }
  }

  async function create(card: CardRequired) {
    if (demoMode.value) return;

    try {
      const { response } = await client.POST("/api/cards", {
        body: toCardPayload(card),
      });
      if (response.status !== 201) {
        message.error("添加卡片失败");
      }
    } catch {
      message.error(NETWORK_ERROR);
    }
  }

  async function update(card: CardRequired): Promise<string | null> {
    if (demoMode.value) return null;

    const body: UpdateCardRequest = toCardPayload(card);
    try {
      const { error } = await client.PUT("/api/cards/{cardId}", {
        params: {
          path: {
            cardId: body.uuid,
          },
        },
        body,
      });
      return error ? SETTINGS_SAVE_FAILED : null;
    } catch {
      return NETWORK_ERROR;
    }
  }

  async function remove(cardId: string) {
    if (demoMode.value) {
      cards.value = cards.value.filter((it) => it.id !== cardId);
      return;
    }

    try {
      const { response } = await client.DELETE("/api/cards/{cardId}", {
        params: {
          path: {
            cardId,
          },
        },
      });
      if (response.status === 404) {
        message.error("删除失败，数据库找不到相应的内容");
        return;
      }
    } catch {
      message.error(NETWORK_ERROR);
      return;
    }

    cards.value = cards.value.filter((it) => it.id !== cardId);
  }

  return { cards, loading, load, create, update, remove };
}

export async function deleteAllCards(): Promise<string | null> {
  try {
    const { error } = await client.DELETE("/api/cards");
    return error ? "删除卡片失败" : null;
  } catch {
    return NETWORK_ERROR;
  }
}
