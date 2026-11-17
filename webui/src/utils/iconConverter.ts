import { NIcon } from "naive-ui";
import { type Component, h } from "vue";

export function iconConverter(icon: Component) {
  return () => {
    return h(NIcon, null, {
      default: () => h(icon),
    });
  };
}
