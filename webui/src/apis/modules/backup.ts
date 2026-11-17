import { client } from "@/apis/client.ts";

export function exportBackup(sections: readonly string[]) {
  return client.GET("/api/backup/export", {
    params: { query: { sections: sections.join(",") } },
    parseAs: "blob",
  });
}

export function inspectBackup(file: File) {
  const body = new FormData();
  body.append("file", file);
  return client.POST("/api/backup/inspect", { body });
}

export function restoreBackup(file: File, sections: readonly string[]) {
  const body = new FormData();
  body.append("file", file);
  return client.POST("/api/backup/restore", {
    body,
    params: { query: { sections: sections.join(",") } },
  });
}
