#!/usr/bin/env bash

# stdin을 반드시 비운다 — 읽지 않으면 Claude Code 쪽 writer가 SIGPIPE로 죽는다.
input=$(cat)

# 프로젝트 루트는 이 스크립트 위치에서 유도한다.
# (절대경로 하드코딩 시 프로젝트 이동·클론·한글경로에서 조용히 깨짐)
root=$(cd "$(dirname "$0")/.." && pwd)

# 1. Git branch
branch=$(git -C "$root" symbolic-ref --short HEAD 2>/dev/null || echo "detached")

# 2. .cs file count under Assets/Scripts
cs_count=$(find "$root/Assets/Scripts" -name "*.cs" 2>/dev/null | wc -l | tr -d ' ')

# 3. Modified/untracked file count
modified=$(git -C "$root" status --porcelain 2>/dev/null | grep -c "^.M\|^M\|^A\|^??" | tr -d ' ')

# 4. Last commit time (relative)
last_commit=$(git -C "$root" log -1 --format="%cr" 2>/dev/null || echo "no commits")

# 5. Test file count
test_count=$(find "$root/Assets/Tests" -name "*.cs" 2>/dev/null | wc -l | tr -d ' ')

printf " %s | cs:%s | tests:%s | changed:%s | %s" "$branch" "$cs_count" "$test_count" "$modified" "$last_commit"
