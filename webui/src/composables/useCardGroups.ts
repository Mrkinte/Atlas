import { ref } from "vue";
import { v4 as uuidv4 } from "uuid";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { client } from "@/apis/client.ts";
import type { components } from "@/apis/schema";
import type { CardGroup } from "@/cards/types.ts";
import { NETWORK_ERROR, SETTINGS_SAVE_FAILED } from "@/config/messages.ts";
import { demoMode } from "@/stores/demo.ts";

type CardGroupResponse = components["schemas"]["CardGroupResponse"];
type CreateCardGroupRequest = components["schemas"]["CreateCardGroupRequest"];
type UpdateCardGroupRequest = components["schemas"]["UpdateCardGroupRequest"];

function toCardGroup(group: CardGroupResponse): CardGroup {
  return {
    id: group.uuid,
    name: group.name,
    sortOrder: group.sortOrder,
    collapsed: group.collapsed,
  };
}

export function useCardGroups() {
  const message = useAppMessage();
  const groups = ref<CardGroup[]>([]);
  const loading = ref(false);

  async function load() {
    loading.value = true;
    try {
      const { data, error } = await client.GET("/api/card-groups");
      if (error) {
        message.error("加载分组失败");
        return;
      }

      groups.value = data
        .map((group: CardGroupResponse) => toCardGroup(group))
        .sort((a, b) => a.sortOrder - b.sortOrder);
    } catch {
      message.error(NETWORK_ERROR);
    } finally {
      loading.value = false;
    }
  }

  async function create(name: string): Promise<CardGroup | null> {
    if (demoMode.value) return null;

    const body: CreateCardGroupRequest = { uuid: uuidv4(), name };
    try {
      const { data, response } = await client.POST("/api/card-groups", { body });
      if (response.status !== 201 || !data) {
        message.error("添加分组失败");
        return null;
      }

      const group = toCardGroup(data);
      groups.value.push(group);
      return group;
    } catch {
      message.error(NETWORK_ERROR);
      return null;
    }
  }

  async function update(group: CardGroup): Promise<string | null> {
    if (demoMode.value) return null;

    const body: UpdateCardGroupRequest = {
      name: group.name,
      sortOrder: group.sortOrder,
      collapsed: group.collapsed,
    };
    try {
      const { error } = await client.PUT("/api/card-groups/{groupId}", {
        params: {
          path: {
            groupId: group.id,
          },
        },
        body,
      });
      return error ? SETTINGS_SAVE_FAILED : null;
    } catch {
      return NETWORK_ERROR;
    }
  }

  async function remove(groupId: string): Promise<string | null> {
    if (demoMode.value) return null;

    try {
      const { response, error } = await client.DELETE("/api/card-groups/{groupId}", {
        params: {
          path: {
            groupId,
          },
        },
      });
      if (response.status === 400) return "至少保留一个分组";
      if (response.status === 404) return "分组不存在";
      return error ? SETTINGS_SAVE_FAILED : null;
    } catch {
      return NETWORK_ERROR;
    }
  }

  return { groups, loading, load, create, update, remove };
}

export async function deleteAllGroups(): Promise<string | null> {
  try {
    const { error } = await client.DELETE("/api/card-groups");
    return error ? "删除分组失败" : null;
  } catch {
    return NETWORK_ERROR;
  }
}
