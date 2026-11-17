#!/usr/bin/env bash
#
# Atlas 本地构建与发版入口
#
# 版本号唯一来源是 git tag（vX.Y.Z），解析规则与仓库根目录 Directory.Build.targets 保持一致。
# 正式发版只创建并推送标签，编译、打包与 Release 发布由 GitHub Actions 完成。
#
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR" || exit 1

WEBUI_DIR="$ROOT_DIR/webui"
ATLAS_PROJECT="$ROOT_DIR/src/atlas.csproj"
PUBLISH_DIR="$ROOT_DIR/bin"

MODE="dev"
MODE_SET=0
RUNTIME="linux-x64"
PREVIEW_ONLY=0
VERSION=""
BASE_TAG=""
RANGE=""
BUMP=""
NEXT_VERSION=""

log() { printf '%s\n' "$*"; }
die() {
    printf '错误: %s\n' "$*" >&2
    exit 1
}

usage() {
    cat <<'EOF'
用法: bash script/preview-build.sh [选项]

模式:
  --dev                构建开发版（默认）。标签提交上为 X.Y.Z，标签之后为 X.Y.Z-dev
  --release            发布正式版。按约定式提交计算下一个版本，确认后创建并推送标签
  --preview            只预览版本号，不做构建与变更（可配合 --release 预览下一个版本）

选项:
  -r, --runtime RID    开发版构建目标运行时，默认 linux-x64，例如 linux-x64、linux-arm64、win-x64
  -h, --help           显示本帮助

流程:
  1. 解析并预览版本号，人工确认后才继续
  2. 开发版依次询问是否编译 webui、atlas，产物输出到 bin/
  3. 正式版只创建并推送标签，编译、打包与发布交给 GitHub Actions

版本约定:
  - 唯一来源是 git tag（vX.Y.Z），规则与 Directory.Build.targets 一致
  - 正式发版要求 main 分支、工作区干净，提交信息遵循约定式提交：
    BREAKING CHANGE 或 feat!: -> major，feat -> minor，其余 -> patch

示例:
  bash script/preview-build.sh                        # 开发版构建，逐项确认
  bash script/preview-build.sh --preview              # 只看开发版版本号
  bash script/preview-build.sh --release --preview    # 只看下一个正式版本号
  bash script/preview-build.sh --release              # 计算版本并推送标签
  bash script/preview-build.sh -r linux-arm64         # 交叉构建 linux-arm64
EOF
}

ask_yes() {
    local prompt="$1" answer
    if ! read -r -p "$prompt [Y/n]: " answer; then
        printf '\n错误: 无法读取确认输入，请使用交互式终端运行\n' >&2
        exit 1
    fi
    answer="${answer:-y}"
    case "$answer" in
        [yY] | [yY][eE][sS]) return 0 ;;
        *) return 1 ;;
    esac
}

# ---------------------------------------------------------------------------
# 版本解析（与 Directory.Build.targets 同一套规则）
# ---------------------------------------------------------------------------
require_git() {
    command -v git >/dev/null 2>&1 || die "未找到 git"
    git rev-parse --git-dir >/dev/null 2>&1 || die "当前目录不是 git 仓库"
}

last_tag() {
    git describe --tags --match 'v*' --abbrev=0 2>/dev/null || true
}

dev_version() {
    local tag
    if tag="$(git describe --tags --exact-match --match 'v*' HEAD 2>/dev/null)"; then
        printf '%s\n' "${tag#v}"
        return 0
    fi
    tag="$(last_tag)"
    if [ -n "$tag" ]; then
        printf '%s\n' "${tag#v}-dev"
    else
        printf '0.0.0-dev\n'
    fi
}

check_release_env() {
    local branch
    branch="$(git branch --show-current)"
    if [ "$branch" != "main" ]; then
        die "只能在 main 分支发版，当前为 ${branch:-detached}"
    fi
    if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
        die "工作区有未提交改动，请先提交或暂存"
    fi
    git fetch --tags --quiet || die "拉取远端标签失败"
}

compute_release_version() {
    local tag range base major minor patch subjects subject_log

    tag="$(last_tag)"
    if [ -z "$tag" ]; then
        if [ -z "$(git log --format=%s -n 1)" ]; then
            die "仓库没有任何提交"
        fi
        BASE_TAG=""
        RANGE="HEAD"
        BUMP="首个版本"
        NEXT_VERSION="1.0.0"
        return 0
    fi

    range="${tag}..HEAD"
    if [ -z "$(git log --format=%s "$range")" ]; then
        die "自 ${tag} 以来没有新提交，无需发版"
    fi

    BASE_TAG="$tag"
    RANGE="$range"
    base="${tag#v}"
    base="${base%%-*}"
    IFS=. read -r major minor patch <<<"$base"
    major="${major:-0}"
    minor="${minor:-0}"
    patch="${patch:-0}"

    subjects="$(git log --format=%s "$range")"
    subject_log="$(git log --format='%s%n%b' "$range")"
    if grep -qE 'BREAKING CHANGE|^[[:alnum:]_()-]+!:' <<<"$subject_log"; then
        BUMP="major"
        major=$((major + 1))
        minor=0
        patch=0
    elif grep -qE '^feat' <<<"$subjects"; then
        BUMP="minor"
        minor=$((minor + 1))
        patch=0
    else
        BUMP="patch"
        patch=$((patch + 1))
    fi
    NEXT_VERSION="${major}.${minor}.${patch}"
}

# ---------------------------------------------------------------------------
# 参数解析
# ---------------------------------------------------------------------------
while [ $# -gt 0 ]; do
    case "$1" in
        -h | --help)
            usage
            exit 0
            ;;
        --dev)
            if [ "$MODE_SET" = 1 ] && [ "$MODE" != "dev" ]; then
                die "--dev 与 --release 不能同时使用"
            fi
            MODE="dev"
            MODE_SET=1
            ;;
        --release)
            if [ "$MODE_SET" = 1 ] && [ "$MODE" != "release" ]; then
                die "--dev 与 --release 不能同时使用"
            fi
            MODE="release"
            MODE_SET=1
            ;;
        --preview | --dry-run)
            PREVIEW_ONLY=1
            ;;
        -r | --runtime)
            [ $# -ge 2 ] || die "选项 $1 需要指定运行时，例如 linux-x64"
            RUNTIME="$2"
            shift
            ;;
        --runtime=*)
            RUNTIME="${1#*=}"
            ;;
        *)
            die "未知参数: $1（使用 --help 查看用法）"
            ;;
    esac
    shift
done

[ -n "$RUNTIME" ] || die "--runtime 不能为空"
require_git

# ---------------------------------------------------------------------------
# 版本预览与确认
# ---------------------------------------------------------------------------
if [ "$MODE" = "release" ]; then
    check_release_env
    compute_release_version
    VERSION="$NEXT_VERSION"

    log "发布模式：正式版"
    log "上一版本：${BASE_TAG:-（无，首个版本）}"
    if [ -n "$BASE_TAG" ]; then
        log "提交范围：${RANGE}（$(git rev-list --count "$RANGE") 个提交）"
    fi
    log "变更类型：${BUMP}"
    log "新版本：  v${VERSION}"

    if [ "$PREVIEW_ONLY" = "1" ]; then
        log "预览模式，未创建标签。"
        exit 0
    fi

    ask_yes "确认创建并推送标签 v${VERSION}?" || die "已取消发版"
    if git rev-parse -q --verify "refs/tags/v${VERSION}" >/dev/null; then
        die "标签 v${VERSION} 已存在"
    fi
    git tag -a "v${VERSION}" -m "v${VERSION}"
    git push origin "v${VERSION}"
    log "标签 v${VERSION} 已推送，构建与发布由 GitHub Actions 执行"
    exit 0
fi

VERSION="$(dev_version)"

log "构建模式：开发版"
log "当前分支：$(git branch --show-current)"
log "最近标签：$(last_tag)"
if [ -n "$(git describe --tags --exact-match --match 'v*' HEAD 2>/dev/null)" ]; then
    log "当前提交正好在标签上，产物版本为正式版本。"
fi
if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
    log "工作区有未提交改动（不会体现在版本号里）。"
fi
log "构建版本：${VERSION}"

if [ "$PREVIEW_ONLY" = "1" ]; then
    log "预览模式，未执行构建。"
    exit 0
fi

ask_yes "确认使用版本 ${VERSION} 继续构建?" || die "已取消构建"

# ---------------------------------------------------------------------------
# 本地构建
# ---------------------------------------------------------------------------
if ask_yes "是否编译 webui?"; then
    [ -d "$WEBUI_DIR" ] || die "目录不存在: $WEBUI_DIR"
    (cd "$WEBUI_DIR" && npm run build) || die "webui 编译失败"
else
    log "跳过 webui 编译"
fi

if ask_yes "是否编译 atlas（${RUNTIME}，Release）?"; then
    rm -rf "$PUBLISH_DIR"
    dotnet publish "$ATLAS_PROJECT" -r "$RUNTIME" -c Release -o "$PUBLISH_DIR" || die "atlas 编译失败"
else
    log "跳过 atlas 编译"
fi

log ""
log "完成：版本 ${VERSION}（${RUNTIME}）"
if [ -d "$PUBLISH_DIR" ]; then
    log "产物目录：$PUBLISH_DIR"
fi
