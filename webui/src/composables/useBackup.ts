import {
  exportBackup as exportBackupApi,
  inspectBackup as inspectBackupApi,
  restoreBackup as restoreBackupApi,
} from "@/apis/modules/backup.ts";
import { extractErrorMessage } from "@/apis/errors.ts";
import { NETWORK_ERROR } from "@/config/messages.ts";
import { downloadBlob } from "@/utils/download.ts";

/** 与后端 BackupSection 的分段名一致 */
export type BackupSection = "cards" | "settings" | "images" | "live2d";

export const BACKUP_SECTIONS: BackupSection[] = ["cards", "settings", "images", "live2d"];

export type BackupInspectResult = {
  version: number;
  appVersion: string;
  exportedAt: string;
  sections: BackupSection[];
  cards: number;
  images: number;
  live2DConfigs: number;
};

export type BackupRestoreResult = {
  cards: number;
  images: number;
  files: number;
  live2DConfigs: number;
  settings: boolean;
};

function isBackupSection(value: string): value is BackupSection {
  return BACKUP_SECTIONS.includes(value as BackupSection);
}

function timestamp(): string {
  return new Date().toISOString().replace(/[-:T]/g, "").slice(0, 14);
}

/** 从 Content-Disposition 中取出文件名，取不到时使用本地时间戳 */
function resolveFileName(disposition: string | null): string {
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  return match?.[1]?.trim() || `atlas-backup-${timestamp()}.zip`;
}

export async function exportBackup(sections: readonly BackupSection[]): Promise<string | null> {
  try {
    const { data, error, response } = await exportBackupApi(sections);
    if (!response.ok) {
      // 上传文件总量超限时后端返回具体原因（ProblemDetails.detail）
      return extractErrorMessage(error, "导出备份失败", response.status);
    }

    downloadBlob(data as Blob, resolveFileName(response.headers.get("Content-Disposition")));
    return null;
  } catch {
    return NETWORK_ERROR;
  }
}

export async function inspectBackup(
  file: File,
): Promise<{ data?: BackupInspectResult; error?: string }> {
  try {
    const { data, error, response } = await inspectBackupApi(file);
    if (!response.ok) {
      return {
        error: extractErrorMessage(error, "无法读取备份文件", response.status),
      };
    }

    const result = data as BackupInspectResult;
    return { data: { ...result, sections: result.sections.filter(isBackupSection) } };
  } catch {
    return { error: NETWORK_ERROR };
  }
}

export async function restoreBackup(
  file: File,
  sections: readonly BackupSection[],
): Promise<{ data?: BackupRestoreResult; error?: string }> {
  try {
    const { data, error, response } = await restoreBackupApi(file, sections);

    if (!response.ok) {
      // 校验失败时后端返回具体原因（ProblemDetails.detail）
      return {
        error: extractErrorMessage(error, "恢复失败", response.status),
      };
    }

    return { data: data as BackupRestoreResult };
  } catch {
    return { error: NETWORK_ERROR };
  }
}
