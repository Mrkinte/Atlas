import { onBeforeUnmount, onMounted, ref } from "vue";
import type { components } from "@/apis/schema";
import { getDockerStatus } from "@/apis/modules/docker.ts";

export type DockerStatus = components["schemas"]["DockerStatusResponse"];
export type DockerContainer = components["schemas"]["DockerContainerInfo"];

const POLL_MS = 5000;

export function useDockerStatus() {
  const status = ref<DockerStatus | null>(null);
  const loading = ref(false);
  const failed = ref(false);

  let timer: number | undefined;

  async function refresh() {
    if (loading.value) return;

    loading.value = true;
    try {
      const { data, response } = await getDockerStatus();
      if (!response.ok || !data) {
        failed.value = true;
        return;
      }

      status.value = data;
      failed.value = false;
    } catch {
      failed.value = true;
    } finally {
      loading.value = false;
    }
  }

  onMounted(() => {
    void refresh();
    timer = window.setInterval(() => void refresh(), POLL_MS);
  });

  onBeforeUnmount(() => {
    if (timer !== undefined) window.clearInterval(timer);
  });

  return { status, loading, failed, refresh };
}
