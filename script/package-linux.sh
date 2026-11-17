#!/usr/bin/env bash
# 生成单个 Linux 架构的 deb / fpk / tar.gz
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

ARCH=""
VERSION=""
PAYLOAD=""
OUT_DIR="$ROOT_DIR/dist"

die() {
    printf '错误: %s\n' "$*" >&2
    exit 1
}

usage() {
    cat <<'EOF'
用法: bash script/package-linux.sh --arch <x64|arm64> --version <X.Y.Z> [选项]

选项:
  --arch ARCH        目标架构，x64 或 arm64
  --version VERSION  包版本，纯 X.Y.Z
  --payload DIR      发布产物目录，默认 bin/<rid>
  --out DIR          输出目录，默认 dist
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --arch)
            [ $# -ge 2 ] || die "选项 $1 需要指定架构"
            ARCH="$2"
            shift
            ;;
        --version)
            [ $# -ge 2 ] || die "选项 $1 需要指定版本"
            VERSION="$2"
            shift
            ;;
        --payload)
            [ $# -ge 2 ] || die "选项 $1 需要指定目录"
            PAYLOAD="$2"
            shift
            ;;
        --out)
            [ $# -ge 2 ] || die "选项 $1 需要指定目录"
            OUT_DIR="$2"
            shift
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *)
            die "未知参数: $1（使用 --help 查看用法）"
            ;;
    esac
    shift
done

case "$ARCH" in
    x64) RID="linux-x64"; DEB_ARCH="amd64"; FPK_PLATFORM="x86" ;;
    arm64) RID="linux-arm64"; DEB_ARCH="arm64"; FPK_PLATFORM="arm" ;;
    *) die "--arch 只支持 x64 或 arm64" ;;
esac

[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "版本必须形如 X.Y.Z，当前为 ${VERSION}"
PAYLOAD="${PAYLOAD:-$ROOT_DIR/bin/$RID}"
[ -d "$PAYLOAD" ] || die "发布目录不存在: $PAYLOAD"
[ -f "$PAYLOAD/atlas" ] || die "发布目录缺少可执行文件 atlas: $PAYLOAD"
# Actions 制品经 ZIP 流转会丢掉可执行位，下载回来是 644，装完 systemd 会 203/EXEC
chmod 755 "$PAYLOAD/atlas"

BUILD_DIR="$ROOT_DIR/.build/$ARCH"
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR" "$OUT_DIR"

cp -a package/deb "$BUILD_DIR/deb"
mkdir -p "$BUILD_DIR/deb/opt/atlas"
rsync -a --exclude '*.pdb' --exclude '*.dbg' "$PAYLOAD/" "$BUILD_DIR/deb/opt/atlas/"
sed -i -e "s/@VERSION@/$VERSION/" -e "s/@DEB_ARCH@/$DEB_ARCH/" "$BUILD_DIR/deb/DEBIAN/control.in"
mv "$BUILD_DIR/deb/DEBIAN/control.in" "$BUILD_DIR/deb/DEBIAN/control"
chmod 755 "$BUILD_DIR/deb"
dpkg-deb --build --root-owner-group "$BUILD_DIR/deb" \
    "$OUT_DIR/atlas-${VERSION}-debian-${DEB_ARCH}.deb"

command -v fnpack >/dev/null 2>&1 || die "未找到 fnpack"
cp -a package/fpk "$BUILD_DIR/fpk"
mkdir -p "$BUILD_DIR/fpk/app/server"
rsync -a --exclude '*.pdb' --exclude '*.dbg' "$PAYLOAD/" "$BUILD_DIR/fpk/app/server/"
sed -i -e "s/@VERSION@/$VERSION/" -e "s/@FPK_PLATFORM@/$FPK_PLATFORM/" "$BUILD_DIR/fpk/manifest.in"
mv "$BUILD_DIR/fpk/manifest.in" "$BUILD_DIR/fpk/manifest"
(cd "$BUILD_DIR/fpk" && fnpack build)
mv "$BUILD_DIR/fpk"/*.fpk "$OUT_DIR/atlas-${VERSION}-fnos-${ARCH}.fpk"

tar -C "$PAYLOAD" -czf "$OUT_DIR/atlas-${VERSION}-${RID}.tar.gz" .

printf '完成: %s\n' "$ARCH"
ls -1 "$OUT_DIR"
