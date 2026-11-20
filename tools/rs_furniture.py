# -*- coding: utf-8 -*-
"""RewardSet.FurniturelD 全表反查：33 家具缺口的事件奖励覆盖验证"""
import json, os

p = 'E:/Game/Mod/survivallog-mods/gamekb/2026-11-12_26615062/tables'

def load(n):
    d = json.load(open(f'{p}/{n}.json', encoding='utf-8'))
    data = d.get('data') if isinstance(d, dict) else d
    if not isinstance(data, list):
        return []
    return [{k.replace('_k__BackingField', ''): v for k, v in r.items()} for r in data]

rs = {r['ID']: r for r in load('Config_RewardSet')}
fur_from_rs = {}
for fn in sorted(os.listdir(p)):
    if not fn.endswith('.json') or fn == 'Config_RewardSet.json':
        continue
    for row in load(fn[:-5]):
        rid0 = row.get('ID')
        for k, v in row.items():
            if 'RewardSet' not in k:
                continue
            for rid in (v if isinstance(v, list) else [v] if isinstance(v, int) else []):
                r = rs.get(rid)
                if not r:
                    continue
                for fid in (r.get('FurniturelD') or []):
                    if isinstance(fid, int) and fid > 0:
                        fur_from_rs.setdefault(fid, []).append(f'{fn[:-5]}[{rid0}].{k}->{rid}')

furn = load('Config_Furniture')
def truthy(v): return v is True or v == 1 or v == '1' or v == 'True'
furn_in = [f for f in furn if truthy(f.get('InCodex'))]
miss = [f for f in furn_in if not f.get('ShopPage') and f['ID'] not in fur_from_rs]
print('缺口(ShopPage=0) 家具总数:', len([f for f in furn_in if not f.get('ShopPage')]))
print('RewardSet.FurniturelD 覆盖后仍未覆盖:', len(miss))
for m in miss:
    print(' ', m['ID'], m.get('Name_Local'))
print('覆盖样例(9300):', fur_from_rs.get(9300))
