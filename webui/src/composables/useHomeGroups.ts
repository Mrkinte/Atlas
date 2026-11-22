import { computed } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { useCardGroups } from "@/composables/useCardGroups.ts";
import type { CardGroup } from "@/cards/types.ts";
import { CARD_GROUP_NAME_MAX_LENGTH } from "@/config/default.ts";

export function useHomeGroups() {
  const { groups, loading, load, create, update, remove } = useCardGroups();
  const message = useAppMessage();

  // sort 会重排原数组，先拷贝避免直接改动 groups
  const sortedGroups = computed(() => [...groups.value].sort((a, b) => a.sortOrder - b.sortOrder));

  async function addGroup() {
    await create("");
  }

  async function renameGroup(group: CardGroup, name: string) {
    const next = name.trim().slice(0, CARD_GROUP_NAME_MAX_LENGTH);
    if (next === group.name) return;

    const previous = group.name;
    group.name = next;
    const err = await update(group);
    if (err) {
      group.name = previous;
      message.error(err);
    }
  }

  async function toggleCollapsed(group: CardGroup) {
    group.collapsed = !group.collapsed;
    const err = await update(group);
    if (err) {
      group.collapsed = !group.collapsed;
      message.error(err);
    }
  }

  async function moveGroup(group: CardGroup, direction: -1 | 1) {
    const index = sortedGroups.value.indexOf(group);
    if (index < 0) return;

    const target = sortedGroups.value[index + direction];
    if (!target) return;

    const order = group.sortOrder;
    group.sortOrder = target.sortOrder;
    target.sortOrder = order;

    const [groupError, targetError] = await Promise.all([update(group), update(target)]);
    const err = groupError ?? targetError;
    if (err) {
      message.error(err);
      await load();
    }
  }

  async function removeGroup(group: CardGroup) {
    const err = await remove(group.id);
    if (err) {
      message.error(err);
      return false;
    }

    groups.value = groups.value.filter((item) => item.id !== group.id);
    message.success("已删除分组");
    return true;
  }

  return {
    groups,
    sortedGroups,
    loading,
    load,
    addGroup,
    renameGroup,
    toggleCollapsed,
    moveGroup,
    removeGroup,
  };
}
