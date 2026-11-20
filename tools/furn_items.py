# -*- coding: utf-8 -*-
"""33 缺口家具 → Item.TargetFurnitureID 物品 → ItemSourceIndex 来源覆盖验证"""
import json
from collections import defaultdict

p = 'E:/Game/Mod/survivallog-mods/gamekb/2026-11-12_26615062/tables'

def load(n):
    d = json.load(open(f'{p}/{n}.json', encoding='utf-8'))
    return [{k.replace('_k__BackingField', ''): v for k, v in r.items()} for r in d['data']]

def truthy(v): return v is True or v == 1 or v == '1' or v == 'True'
def num(v, d=0):
    try: return int(v)
    except (TypeError, ValueError): return d

items = load('Config_Item'); item_by = {r['ID']: r for r in items}
recipes = load('Config_CookingRecipe'); prod = load('Config_ProductionList')
shops = load('Config_Shop'); stocks = load('Config_StrangerStock')
rgroups = load('Config_RandomGroup'); loots = load('Config_GroundLoot')
plants = load('Config_Plant'); pkgs = load('Config_Package'); evs = load('Config_Event')
furn = load('Config_Furniture')
rg_by = {r['ID']: r for r in rgroups}
def rg_items(g):
    r = rg_by.get(g)
    return [x for x in (r.get('IdList') or []) if isinstance(x, int)] if r else []

src = defaultdict(list)
def add(i, tag):
    if isinstance(i, int) and i > 0: src[i].append(tag)
for r in recipes:
    for k in ('PerfectItemID', 'GoodItemID', 'NormalItemID', 'FailItemID'): add(r.get(k), '烹饪')
for x in prod:
    for k in ('ProductID', 'perfect_item_id', 'FailedID'):
        for i in (x.get(k) or []): add(i, '制造')
for s in shops:
    for i in (s.get('ItemIdList') or []): add(i, '商店')
    for g in (s.get('RandomID') or []):
        for i in rg_items(g): add(i, '商店随机')
for st in stocks:
    for i in (st.get('ItemIdList') or []): add(i, '老主顾')
for gl in loots:
    for i in rg_items(num(gl.get('RandomGroupId'))): add(i, '拾取')
for pl in plants:
    for k in ('Gain', 'Perfect_Gain', 'Gain_Seed', 'WitheredGain'):
        v = pl.get(k)
        for i in (v if isinstance(v, list) else []): add(i, '种植')
for pk in pkgs:
    for i in (pk.get('ItemIdList') or []): add(i, '包裹')
    for g in (pk.get('RandomID') or []):
        for i in rg_items(g): add(i, '包裹随机')
# 事件物品奖励：Event.ItemRequire? Event 三个 RewardSetId → RewardSet.ItemChange/RandomGroupId
rs = {r['ID']: r for r in load('Config_RewardSet')}
for e in evs:
    rsids = set()
    for k in ('StartRewardSetId', 'RewardSetId', 'ExpiredRewardSetId'):
        v = e.get(k)
        rsids.update(v if isinstance(v, list) else [v] if isinstance(v, int) else [])
    for rid in rsids:
        r = rs.get(rid)
        if not r: continue
        for i in (r.get('ItemChange') or []): add(i, '事件奖励')
        for i in (r.get('ItemPackage') or []): add(i, '事件奖励')
        rgv = r.get('RandomGroupId')
        for g in (rgv if isinstance(rgv, list) else [rgv] if isinstance(rgv, int) else []):
            for i in rg_items(g): add(i, '事件奖励')

miss = [f for f in furn if truthy(f.get('InCodex')) and not f.get('ShopPage')]
tf = {}
for it in items:
    t = it.get('TargetFurnitureID')
    if isinstance(t, int) and t > 0: tf[t] = it['ID']

no_src = []
for f in miss:
    iid = tf.get(f['ID'])
    s = src.get(iid, [])
    if not s: no_src.append((f['ID'], f.get('Name_Local'), iid, item_by.get(iid, {}).get('ItemName_Local')))
print(f'缺口 {len(miss)} 个：TargetFurnitureID 物品仍无来源的 {len(no_src)} 个：')
for x in no_src: print(' ', x)
