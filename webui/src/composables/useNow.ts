import { type MaybeRefOrGetter, onBeforeUnmount, onMounted, ref, toValue, watch } from "vue";

/** smooth 为 true 时按动画帧刷新，否则对齐整秒 */
export function useNow(smooth: MaybeRefOrGetter<boolean> = false) {
  const now = ref(new Date());
  let timer: number | undefined;
  let frame: number | undefined;

  function stop() {
    if (timer !== undefined) {
      window.clearTimeout(timer);
      timer = undefined;
    }
    if (frame !== undefined) {
      window.cancelAnimationFrame(frame);
      frame = undefined;
    }
  }

  /** 对齐到下一个整秒再刷新，避免秒数跳变不均匀 */
  function tickAfterDelay() {
    now.value = new Date();
    timer = window.setTimeout(tickAfterDelay, 1000 - (Date.now() % 1000));
  }

  function tickOnFrame() {
    now.value = new Date();
    frame = window.requestAnimationFrame(tickOnFrame);
  }

  function start() {
    stop();
    if (toValue(smooth)) tickOnFrame();
    else tickAfterDelay();
  }

  watch(() => toValue(smooth), start);
  onMounted(start);
  onBeforeUnmount(stop);

  return now;
}
