# -*- coding: utf-8 -*-
"""40 个反查缺口的逐条归因（排除主键 id 碰撞）：每条缺口列出候选来源表命中"""
import json, os
from collections import defaultdict

T = 'E:/Game/Mod/survivallog-mods/gamekb/2026-11-12_26615062/tables'

def load(n):
    d = json.load(open(f'{T}/{n}.json', encoding='utf-8'))
    return [{k.replace('_k__BackingField', ''): v for k, v in r.items()} for r in d['data']]

def truthy(v): return v is True or v == 1 or v == '1' or v == 'True'
def num(v, d=0):
    try: return int(v)
    except (TypeError, ValueError): return d

items = load('Config_Item'); item_by = {r['ID']: r for r in items}
furns = load('Config_Furniture'); furn_by = {r['ID']: r for r in furns}
food_ids = {r['ID'] for r in items if r.get('Category') == 1 and truthy(r.get('InCodex'))}
furn_ids = {r['ID'] for r in furns if truthy(r.get('InCodex'))}

# 重算缺口（与 coverage.py 同规则简化版：商店/随机组/烹饪/制造/陷阱/拾取/种植/包裹+里程碑奖励）
src = defaultdict(list)
recipes = load('Config_CookingRecipe'); prod = load('Config_ProductionList')
shops = load('Config_Shop'); stocks = load('Config_StrangerStock')
rgroups = load('Config_RandomGroup'); loots = load('Config_GroundLoot')
plants = load('Config_Plant'); traps = load('Config_Trap'); pkgs = load('Config_Package')
ms = load('Config_CodexMilestone')
rg_by = {r['ID']: r for r in rgroups}
def rg_items(gid):
    g = rg_by.get(gid)
    if not g: return []
    for k in ('ItemIDList', 'ItemIdList', 'IdList', 'Items'):
        v = g.get(k)
        if isinstance(v, list): return [x for x in v if isinstance(x, int)]
    return []
def add(iid, tag):
    if isinstance(iid, int): src[iid].append(tag)
for r in recipes:
    for k in ('PerfectItemID', 'GoodItemID', 'NormalItemID', 'FailItemID'): add(r.get(k), '烹饪')
for p in prod:
    for k in ('ProductID', 'perfect_item_id', 'FailedID'): add(p.get(k), '制造')
for s in shops:
    for iid in (s.get('ItemIdList') or []): add(iid, '商店固定')
    for gid in (s.get('RandomID') or []):
        for iid in rg_items(gid): add(iid, '商店随机')
for st in stocks:
    for iid in (st.get('ItemIdList') or []): add(iid, '老主顾')
for gl in loots:
    for iid in rg_items(num(gl.get('RandomGroupId'))): add(iid, '地图拾取')
for pl in plants:
    for k in ('Gain', 'Perfect_Gain', 'Gain_Seed', 'WitheredGain'):
        v = pl.get(k)
        if isinstance(v, int): add(v, '种植')
        elif isinstance(v, list): [add(x, '种植') for x in v if isinstance(x, int)]
for t in traps:
    for pid in (t.get('Prey_Id') or []): add(pid, '陷阱')
for pk in pkgs:
    for iid in (pk.get('ItemIdList') or []): add(iid, '包裹')
    for gid in (pk.get('RandomID') or []):
        for iid in rg_items(gid): add(iid, '包裹随机')
for m in ms:
    if m.get('RewardType') == 2: add(m.get('RewardId'), '里程碑奖励')  # type 语义待实现期校准

miss_food = sorted(i for i in food_ids if not src.get(i))
miss_furn = sorted(i for i in furn_ids if not src.get(i) and num(furn_by[i].get('ShopPage')) == 0)
print(f'补包裹/里程碑后：食物缺口 {len(miss_food)}，家具缺口(非商店页) {len(miss_furn)}')

# 全表扫描归因（排除主键列）
KEYCOLS = {'ID', 'MainKey'}
def scan(target):
    hits = []
    for fn in os.listdir(T):
        if not fn.endswith('.json'): continue
        try: rows = load(fn[:-5])
        except Exception: continue
        for row in rows:
            rid = row.get('ID')
            for k, v in row.items():
                if k in KEYCOLS: continue
                vals = v if isinstance(v, list) else ([v] if isinstance(v, int) else [])
                if target in vals:
                    hits.append(f'{fn[:-5]}[{rid}].{k}')
    return hits

print('\n=== 食物缺口归因 ===')
for i in miss_food:
    h = scan(i)
    print(f'#{i} {item_by[i].get("ItemName_Local")}  Des={str(item_by[i].get("ItemDes1_Local"))[:24]}  <- {h[:4]}')
print('\n=== 家具缺口归因（前20）===')
for i in miss_furn[:20]:
    h = scan(i)
    print(f'#{i} {furn_by[i].get("Name_Local")}  <- {h[:4]}')
