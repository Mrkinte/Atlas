import MarkdownIt from "markdown-it";
import taskLists from "markdown-it-task-lists";
import hljs from "highlight.js/lib/common";

/** 全站共用；html: false 避免卡片内容在匿名首页执行脚本 */
const md = new MarkdownIt({
  html: false,
  linkify: true,
  breaks: true,
  highlight(code, lang) {
    if (lang && hljs.getLanguage(lang)) {
      try {
        return hljs.highlight(code, {
          language: lang,
          ignoreIllegals: true,
        }).value;
      } catch {
        // 高亮失败时返回空串，交给 markdown-it 转义为纯文本
      }
    }
    return "";
  },
});

/** 默认渲染为不可勾选的只读勾选框 */
md.use(taskLists);

/** 新窗口打开，并阻断 referrer 与 opener */
const defaultLinkOpen = md.renderer.rules.link_open;
md.renderer.rules.link_open = (tokens, idx, options, env, renderer) => {
  tokens[idx].attrSet("target", "_blank");
  tokens[idx].attrSet("rel", "noopener noreferrer");

  return defaultLinkOpen
    ? defaultLinkOpen(tokens, idx, options, env, renderer)
    : renderer.renderToken(tokens, idx, options);
};

export function renderMarkdown(source: string): string {
  return md.render(source);
}
