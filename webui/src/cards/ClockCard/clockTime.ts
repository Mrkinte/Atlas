export function pad2(value: number): string {
  return String(value).padStart(2, "0");
}

/** 12 小时制下 0 点显示为 12 */
export function formatHour(date: Date, hour24: boolean): string {
  const hours = date.getHours();
  return pad2(hour24 ? hours : hours % 12 || 12);
}

export function formatMinute(date: Date): string {
  return pad2(date.getMinutes());
}

export function formatSecond(date: Date): string {
  return pad2(date.getSeconds());
}

export function formatPeriod(date: Date): "AM" | "PM" {
  return date.getHours() < 12 ? "AM" : "PM";
}

const WEEKDAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"] as const;

/** MM-DD SUN，像素字体没有中文字形 */
export function formatAsciiDate(date: Date): string {
  return `${pad2(date.getMonth() + 1)}-${pad2(date.getDate())} ${WEEKDAYS[date.getDay()]}`;
}

const chineseDate = new Intl.DateTimeFormat("zh-CN", {
  month: "long",
  day: "numeric",
  weekday: "long",
});

export function formatChineseDate(date: Date): string {
  return chineseDate.format(date);
}
