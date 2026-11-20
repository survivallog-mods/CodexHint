# -*- coding: utf-8 -*-
"""CodexHint 可行性验证：各分类条目数锚点校准 + 反查覆盖率统计
数据源：gamekb/2026-11-12_26615062/tables/*.json（离线镜像，与 dump 同基线）
锚点（截图 2026-11-19）：植物 34/38、猎物 23/23、家具 64/111
"""
import json, os, sys
from collections import defaultdict

T = r'E:/Game/Mod/survivallog-mods/gamekb/2026-11-12_26615062/tables'

def load(name):
    d = json.load(open(f'{T}/{name}.json', encoding='utf-8'))
    out = []
    for row in d['data']:
        out.append({k.replace('_k__BackingField', ''): v for k, v in row.items()})
    return out

def idx(rows, key):
    return {r[key]: r for r in rows}

def num(v, default=0):
    try:
        return int(v)
    except (TypeError, ValueError):
        return default

items = load('Config_Item')
item_by_id = idx(items, 'ID')
recipes = load('Config_CookingRecipe')
prod = load('Config_ProductionList')
shops = load('Config_Shop')
stocks = load('Config_StrangerStock')
plants = load('Config_Plant')
rats = load('Config_SceneRat')
reno = load('Config_RenovationStyle')
furn = load('Config_Furniture')
try:
    rgroups = load('Config_RandomGroup')
except FileNotFoundError:
    rgroups = []
try:
    shopfurn = load('Config_ShopFurniture')
except FileNotFoundError:
    shopfurn = []

# ---------- 1. 分类条目清单（锚点校准） ----------
food_like = [i for i in items if i.get('InCodex')]
plants_codex = [p for p in plants if p.get('InCodex')]
prey_items = [i for i in items if num(i.get('Prey_Rarity')) > 0]
prod_codex = [p for p in prod if p.get('InCodex')]
furn_codex = [f for f in furn if num(f.get('CodexCategoryId')) > 0]
dish_ids = set()
for r in recipes:
    for k in ('PerfectItemID', 'GoodItemID', 'NormalItemID'):
        if r.get(k): dish_ids.add(r[k])

print('=== 锚点校准（截图：植物38 猎物23 家具111 菜肴170+ 食物83+ 制造80+ 装修2+）===')
print(f'InCodex items           : {len(food_like)}   (食物候选)')
print(f'Config_Plant.InCodex    : {len(plants_codex)}   (锚点 38)')
print(f'Prey_Rarity>0 items     : {len(prey_items)}   (锚点 23)')
print(f'ProductionList.InCodex  : {len(prod_codex)}   (锚点 80+)')
print(f'Furniture CodexCat>0    : {len(furn_codex)}   (锚点 111)')
print(f'CookingRecipe 产出 item : {len(dish_ids)}   (锚点 170+)')
print(f'RenovationStyle 全表    : {len(reno)}   (锚点 2+)')
print(f'RandomGroup rows={len(rgroups)}  ShopFurniture rows={len(shopfurn)}')

# 食物到底怎么判定：看 InCodex 物品的 Category 分布
catdist = defaultdict(int)
for i in food_like: catdist[i.get('Category')] += 1
print('InCodex item Category 分布:', dict(catdist))
