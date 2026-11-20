# -*- coding: utf-8 -*-
"""33 家具缺口逐个完整归因：全表扫描值命中（排主键/碰撞），每缺口列全部命中"""
import json, os

p = 'E:/Game/Mod/survivallog-mods/gamekb/2026-11-12_26615062/tables'

def load(n):
    d = json.load(open(f'{p}/{n}.json', encoding='utf-8'))
    data = d.get('data') if isinstance(d, dict) else d
    return [{k.replace('_k__BackingField', ''): v for k, v in r.items()} for r in (data or [])]

furn = load('Config_Furniture')
def truthy(v): return v is True or v == 1 or v == '1' or v == 'True'
miss = [f for f in furn if truthy(f.get('InCodex')) and not f.get('ShopPage')]
targets = {m['ID'] for m in miss}
name = {m['ID']: m.get('Name_Local') for m in miss}

KEYS = {'ID', 'MainKey', 'ConditionSetId', 'TaskConditionSetId', 'VisibilityConditionSetId',
        '_ID_k__BackingField'}  # 后三列=ConditionSet id 同号碰撞源，剔除
hits = {}
for fn in sorted(os.listdir(p)):
    if not fn.endswith('.json'):
        continue
    short = fn[:-5]
    for row in load(short):
        rid = row.get('ID')
        for k, v in row.items():
            if k in KEYS or 'ConditionSet' in k:
                continue
            vals = v if isinstance(v, list) else ([v] if isinstance(v, int) else [])
            for x in vals:
                if x in targets:
                    hits.setdefault(x, []).append(f'{short}[{rid}].{k}')

for m in sorted(miss, key=lambda x: x['ID']):
    h = hits.get(m['ID'], [])
    # 同表同列去重计数
    seen, uniq = set(), []
    for s in h:
        col = s.split('].')[1]
        tab = s.split('[')[0]
        key = f'{tab}.{col}'
        if key not in seen:
            seen.add(key)
            uniq.append(s)
    print(f'{m["ID"]} {m.get("Name_Local")} <- {uniq[:3]}{"…" if len(uniq) > 3 else ""}')
