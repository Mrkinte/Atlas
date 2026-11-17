<script lang="ts" setup>
import { computed, h, nextTick, onMounted, ref } from "vue";
import type { DropdownOption, NInput } from "naive-ui";
import bingIcon from "@/assets/search/bing.svg?url";
import baiduIcon from "@/assets/search/baidu.svg?url";
import googleIcon from "@/assets/search/google.svg?url";
import githubIcon from "@/assets/search/github.svg?url";

interface SearchEngine {
  key: string;
  label: string;
  icon?: string;
  urlTemplate: string;
  placeholder?: string;
}

const SEARCH_ENGINES: SearchEngine[] = [
  {
    key: "bing",
    label: "Bing",
    icon: bingIcon,
    urlTemplate: "https://www.bing.com/search?q={query}",
    placeholder: "使用 Bing 搜索",
  },
  {
    key: "baidu",
    label: "百度",
    icon: baiduIcon,
    urlTemplate: "https://www.baidu.com/s?wd={query}",
    placeholder: "使用 百度 搜索",
  },
  {
    key: "google",
    label: "Google",
    icon: googleIcon,
    urlTemplate: "https://www.google.com/search?q={query}",
    placeholder: "使用 Google 搜索",
  },
  {
    key: "github",
    label: "GitHub",
    icon: githubIcon,
    urlTemplate: "https://github.com/search?q={query}",
    placeholder: "在 GitHub 搜索",
  },
];

const query = ref("");
const inputRef = ref<InstanceType<typeof NInput> | null>(null);
const currentKey = ref("");
const currentEngine = ref<SearchEngine>(SEARCH_ENGINES[0]);

const engineOptions = computed<DropdownOption[]>(() =>
  SEARCH_ENGINES.map((e) => ({
    key: e.key,
    label: e.label,
    icon: e.icon ? () => h("img", { src: e.icon, class: "search-bar__engine-icon" }) : undefined,
  })),
);

const handleSelectEngine = (key: string) => {
  currentEngine.value = SEARCH_ENGINES.find((it) => it.key === key) ?? SEARCH_ENGINES[0];
  localStorage.setItem("preferred-search-engine", key);
  nextTick(() => inputRef.value?.focus());
};

const handleSearch = () => {
  const q = query.value.trim();
  if (!q) return;

  const url = currentEngine.value.urlTemplate.replace("{query}", encodeURIComponent(q));
  window.open(url, "_blank", "noopener, noreferrer");
};

onMounted(() => {
  const savedKey = localStorage.getItem("preferred-search-engine");
  const isValid = savedKey && SEARCH_ENGINES.some((e) => e.key === savedKey);
  currentKey.value = isValid ? savedKey : SEARCH_ENGINES[0].key;
});
</script>

<template>
  <div class="search-card">
    <n-input-group>
      <n-dropdown
        :options="engineOptions"
        size="large"
        trigger="click"
        @select="handleSelectEngine"
      >
        <n-button class="search-bar__engine-btn" size="large">
          <img
            v-if="currentEngine.icon"
            :src="currentEngine.icon"
            alt=""
            class="search-bar__engine-icon"
          />
          <span>{{ currentEngine.label }}</span>
        </n-button>
      </n-dropdown>

      <n-input
        ref="inputRef"
        v-model:value="query"
        :placeholder="currentEngine.placeholder ?? '输入搜索内容'"
        clearable
        size="large"
        @keyup.enter="handleSearch"
      />

      <n-button size="large" type="primary" @click="handleSearch"> 搜索 </n-button>
    </n-input-group>
  </div>
</template>

<style scoped>
.search-card {
  display: flex;
  align-items: center;
  height: 100%;
}

.search-bar__engine-btn {
  display: inline-flex;
  align-items: center;
  min-width: 104px;
  justify-content: center;
}
</style>

<style>
.search-bar__engine-icon {
  width: 16px;
  height: 16px;
  object-fit: contain;
  vertical-align: middle;
  margin-right: 8px;
}
</style>
