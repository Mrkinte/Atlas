import { client } from "@/apis/client.ts";

/** 实时数据走 WebSocket */
export function getHardware() {
  return client.GET("/api/system-status/hardware");
}
