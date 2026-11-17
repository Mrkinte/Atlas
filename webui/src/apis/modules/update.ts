import { client } from "@/apis/client.ts";

export function checkUpdate(refresh = false) {
  return client.GET("/api/update/check", {
    params: { query: refresh ? { refresh: true } : {} },
  });
}
