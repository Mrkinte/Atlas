import { client } from "@/apis/client.ts";
import type { components } from "@/apis/schema";

type UpdateSettingsRequest = components["schemas"]["UpdateSettingsRequest"];

export function getSettings() {
  return client.GET("/api/settings");
}

export function updateSettings(body: UpdateSettingsRequest) {
  return client.PUT("/api/settings", { body });
}
