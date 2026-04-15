#!/usr/bin/env bash

input=$(cat)

# 1. Git branch
branch=$(git -C "C:/Project/곤충게임" symbolic-ref --short HEAD 2>/dev/null || echo "detached")

# 2. .cs file count under Assets/Scripts
cs_count=$(find "C:/Project/곤충게임/Assets/Scripts" -name "*.cs" 2>/dev/null | wc -l | tr -d ' ')

# 3. Modified/untracked file count
modified=$(git -C "C:/Project/곤충게임" status --porcelain 2>/dev/null | grep -c "^.M\|^M\|^A\|^??" | tr -d ' ')

# 4. Last commit time (relative)
last_commit=$(git -C "C:/Project/곤충게임" log -1 --format="%cr" 2>/dev/null || echo "no commits")

# 5. Test file count
test_count=$(find "C:/Project/곤충게임/Assets/Tests" -name "*.cs" 2>/dev/null | wc -l | tr -d ' ')

printf " %s | cs:%s | tests:%s | changed:%s | %s" "$branch" "$cs_count" "$test_count" "$modified" "$last_commit"
