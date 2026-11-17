import js from "@eslint/js";
import pluginVue from "eslint-plugin-vue";
import { defineConfigWithVueTs, vueTsConfigs } from "@vue/eslint-config-typescript";
import globals from "globals";

export default defineConfigWithVueTs(
  {
    // 全局忽略：构建产物与自动生成的声明文件，不参与 lint
    ignores: ["dist/**", "node_modules/**", "components.d.ts", "src/apis/schema.d.ts"],
  },

  // 基础推荐规则（JS 通用部分）
  js.configs.recommended,
  // Vue 模板推荐规则
  pluginVue.configs["flat/recommended"],
  // TypeScript 推荐规则（自动适配 <script lang="ts">）
  vueTsConfigs.recommended,

  {
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        ...globals.browser,
        ...globals.node,
      },
    },
    rules: {
      // ---- 风格降级说明 ----
      // 以下规则只约束排版格式，与 prettier 职责重叠且存量代码不满足，
      // 修复会造成大量纯格式 diff，因此关闭；格式统一交给 prettier。
      "vue/html-indent": "off",
      "vue/html-quotes": "off",
      "vue/html-self-closing": "off",
      "vue/max-attributes-per-line": "off",
      "vue/singleline-html-element-content-newline": "off",
      "vue/multiline-html-element-content-newline": "off",
      "vue/first-attribute-linebreak": "off",
      "vue/html-closing-bracket-newline": "off",
      "vue/html-closing-bracket-spacing": "off",
      "vue/mustache-interpolation-spacing": "off",
      "vue/no-multi-spaces": "off",
      "vue/attribute-hyphenation": "off",
      "vue/v-on-event-hyphenation": "off",
      "vue/arrow-parens": "off",
      "vue/block-order": "off",
      "vue/component-definition-name-casing": "off",

      // ---- 语义类规则按存量代码情况降级 ----
      // 组件文件名多为单词（如 index.vue / App.vue），强制多词会要求批量改名，收益低
      "vue/multi-word-component-names": "off",
      // 存量组件大量使用未声明的透传属性，改造需动业务代码，超出本次范围
      "vue/no-v-html": "warn",
      // 存量代码存在空组件名/异步组件场景，修复需业务评估，先降为警告
      "vue/require-default-prop": "off",
      // props 复杂类型下 default 推导报误报，交由 TS 类型检查保证
      "vue/require-explicit-emits": "off",

      // TS 类型场景下 void 表达式常见于 fire-and-forget，交由代码评审把关
      "@typescript-eslint/no-unused-expressions": "warn",
      // 存量代码使用 any 做透传/动态配置，逐步收敛，先降为警告避免阻塞
      "@typescript-eslint/no-explicit-any": "warn",
      "@typescript-eslint/no-unsafe-function-type": "warn",
      "@typescript-eslint/no-empty-object-type": "warn",
      "@typescript-eslint/no-unused-vars": [
        "warn",
        {
          argsIgnorePattern: "^_",
          varsIgnorePattern: "^_",
          caughtErrorsIgnorePattern: "^_",
        },
      ],
    },
  },
);
