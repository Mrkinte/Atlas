function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function daysInMonth(year: number, month: number): number {
  return new Date(year, month + 1, 0).getDate();
}

/** 2 月 29 日遇到平年时落到当月最后一天 */
function createClampedDate(year: number, month: number, day: number): Date {
  return new Date(year, month, Math.min(day, daysInMonth(year, month)));
}

/** 非法值返回 null */
export function parseDateValue(value: unknown): Date | null {
  if (typeof value !== "string") return null;

  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value.trim());
  if (!match) return null;

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const date = new Date(year, month - 1, day);

  // 排除 2026-02-30 这类会被自动进位的日期
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }

  return date;
}

export function formatDateValue(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

/** 每年重复时取下一次发生日期，今天发生则保留今天 */
export function resolveTargetDate(value: unknown, repeatYearly: boolean, now: Date): Date | null {
  const parsed = parseDateValue(value);
  if (!parsed) return null;
  if (!repeatYearly) return parsed;

  const month = parsed.getMonth();
  const day = parsed.getDate();
  const today = startOfDay(now);

  let target = createClampedDate(today.getFullYear(), month, day);
  if (target.getTime() < today.getTime()) {
    target = createClampedDate(today.getFullYear() + 1, month, day);
  }
  return target;
}

/** 正数为未来，负数为过去，0 为今天 */
export function diffCalendarDays(target: Date, now: Date): number {
  const targetUtc = Date.UTC(target.getFullYear(), target.getMonth(), target.getDate());
  const nowUtc = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  return Math.round((targetUtc - nowUtc) / 86_400_000);
}

export function formatDisplayDate(date: Date): string {
  return `${date.getFullYear()}年${date.getMonth() + 1}月${date.getDate()}日`;
}
