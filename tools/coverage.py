# -*- coding: utf-8 -*-
"""CodexHint 反查覆盖率验证
条目判定（IsInCodex@0x26A8370 汇编实证）：
  Food=Item.Category==1&InCodex | Dish=Recipe存在 | Plant=Plant.InCodex
  Prey=Item.Prey_Rarity>0&InCodex | Craft=ProductionList.InCodex
  Furniture=Furniture.InCodex | Reno=RenovationStyle存在
锚点：食83/191 菜170/525 植34/38 猎23/23 造80/164 具64/111 修2/24
"""
import json
from collections import defaultdict

T = r'E:/Game/Mod/survivallog-mods/gamekb/2026-11-12_26615062/tables'

def load(name):
    d = json.load(open(f'{T}/{name}.json', encoding='utf-8'))
    return [{k.replace('_k__BackingField', ''): v for k, v in r.items()} for r in d['data']]

def truthy(v): return v is True or v == 1 or v == '1' or v == 'True'
def num(v, d=0):
    try: return int(v)
    except (TypeError, ValueError): return d

items = load('Config_Item'); item_by = {r['ID']: r for r in items}
recipes = load('Config_CookingRecipe')
prod = load('Config_ProductionList')
plants = load('Config_Plant')
furns = load('Config_Furniture'); furn_by = {r['ID']: r for r in furns}
reno = load('Config_RenovationStyle')
shops = load('Config_Shop')
stocks = load('Config_StrangerStock')
rgroups = load('Config_RandomGroup'); rg_by = {r['ID']: r for r in rgroups}
loots = load('Config_GroundLoot')
shopfurn = load('Config_ShopFurniture')
ratrows = load('Config_SceneRat')

# ---------- 条目集合 ----------
food_ids = {r['ID'] for r in items if r.get('Category') == 1 and truthy(r.get('InCodex'))}
dish_ids = {r['ID'] for r in recipes}                       # 配方存在即入
plant_ids = {r['ID'] for r in plants if truthy(r.get('InCodex'))}
prey_ids = {r['ID'] for r in items if num(r.get('Prey_Rarity')) > 0 and truthy(r.get('InCodex'))}
craft_ids = {r['ID'] for r in prod if truthy(r.get('InCodex'))}
furn_ids = {r['ID'] for r in furns if truthy(r.get('InCodex'))}
reno_ids = {r['ID'] for r in reno}

# ---------- item → 来源 反查索引 ----------
def rg_items(rgid):
    g = rg_by.get(rgid)
    if not g: return []
    out = []
    for k in ('ItemIDList', 'ItemIdList', 'Items', 'ItemIds'):
        v = g.get(k)
        if isinstance(v, list):
            out += [x for x in v if isinstance(x, int)]
    return out

src = defaultdict(list)  # itemId -> [(tag, text)]
def add(iid, tag, text):
    if isinstance(iid, int):
        src[iid].append((tag, text))

# 烹饪产出
for r in recipes:
    nm = r.get('RecipeName_Local') or f"配方{r['ID']}"
    for k, q in (('PerfectItemID', '完美'), ('GoodItemID', '优良'), ('NormalItemID', '普通'), ('FailItemID', '失败')):
        add(r.get(k), '烹饪', f'烹饪「{nm}」产出（{q}档）')
# 制造产出
for p in prod:
    nm = p.get('ShopName_Local') or f"配方{p['ID']}"
    for k, q in (('ProductID', ''), ('perfect_item_id', '完美'), ('FailedID', '失败')):
        add(p.get(k), '制造', f'制造「{nm}」产出{q and f"（{q}）" or ""}')
# 商店（固定 + 随机组）
for s in shops:
    nm = s.get('ShopName_Local') or s.get('ShopName') or f"商店{s['ID']}"
    for iid in (s.get('ItemIdList') or []):
        if isinstance(iid, int): add(iid, '商店', f'商店「{nm}」有售')
    for rgid in (s.get('RandomID') or []):
        for iid in rg_items(rgid):
            add(iid, '商店', f'商店「{nm}」随机上架')
# 老主顾库存
for st in stocks:
    for iid in (st.get('ItemIdList') or []):
        if isinstance(iid, int): add(iid, '老主顾', '老主顾随机出售')
# 地面拾取
for gl in loots:
    pos = gl.get('PositionName') or ''
    for iid in rg_items(num(gl.get('RandomGroupId'))):
        add(iid, '拾取', f'地图拾取{pos and f"（{pos}）" or ""}')
# 种植收获
for pl in plants:
    nm = pl.get('Name_Local') or f"植物{pl['ID']}"
    for k, q in (('Gain', '收获'), ('Perfect_Gain', '完美收获'), ('Gain_Seed', '收种'), ('WitheredGain', '枯萎')):
        v = pl.get(k)
        if isinstance(v, int): add(v, '种植', f'种植「{nm}」{q}获得')
        elif isinstance(v, list): [add(x, '种植', f'种植「{nm}」{q}获得') for x in v if isinstance(x, int)]
# 陷阱/场景鼠掉落
for rt in ratrows:
    nm = rt.get('SceneRatName_Local') or ''
    # SceneRat 本身不是掉落来源，Prey 解锁靠遭遇；此处不动 src
# 陷阱捕获：Config_Trap.Prey_Id（列表）→ 猎物 item；TrapBait.Prey_Id（列表）补充诱饵
traps = load('Config_Trap')
for t in traps:
    tn = t.get('TrapName_Local') or t.get('Name_Local') or f"陷阱{t['ID']}"
    for pid in (t.get('Prey_Id') or []):
        if isinstance(pid, int): add(pid, '陷阱', f'用「{tn}」捕获')
baits = load('Config_TrapBait')
for b in baits:
    bn = b.get('Des_name_Local') or ''
    for pid in (b.get('Prey_Id') or []):
        if isinstance(pid, int): add(pid, '陷阱', f'陷阱捕获（可用诱饵：{bn}）')
# 家具购买渠道（ShopFurniture: 哪些商店卖哪些家具）
furn_buy = defaultdict(list)
for sf in shopfurn:
    sid = sf.get('ShopID') or sf.get('ID')
    fl = sf.get('FurnitureIDList') or sf.get('FurnitureIdList') or []
    sn = ''
    for s in shops:
        if s['ID'] == sid: sn = s.get('ShopName_Local') or ''
    for fid in (fl if isinstance(fl, list) else []):
        if isinstance(fid, int): furn_buy[fid].append(sn or f'商店{sid}')

# ---------- 覆盖率 ----------
def cov(name, ids, covered):
    total = len(ids); hit = sum(1 for i in ids if covered(i))
    print(f'{name:12s} {hit:4d}/{total:4d}  {hit*100//max(total,1)}%')
    return [i for i in ids if not covered(i)]

print('=== 反查覆盖率 ===')
miss_food = cov('食物', food_ids, lambda i: len(src.get(i, [])) > 0)
cov('菜肴', dish_ids, lambda r: True)   # 配方本身即方案：名称+食材+等级
seed_of = defaultdict(list)   # plantId -> 种子 itemId 列表
for it in items:
    pv = num(it.get('Plant'))
    if pv > 0: seed_of[pv].append(it['ID'])
miss_plant = cov('植物', plant_ids, lambda p: any(len(src.get(s, [])) > 0 for s in seed_of.get(p, [])))
miss_prey = cov('猎物', prey_ids, lambda i: len(src.get(i, [])) > 0)
cov('制造', craft_ids, lambda r: True)  # 配方本身即方案
miss_furn = cov('家具', furn_ids, lambda f: f in furn_buy or len(src.get(f, [])) > 0 or num(furn_by.get(f, {}).get('ShopPage')) > 0)
miss_reno = cov('装修', reno_ids, lambda rid: any((r.get('SourceText_Local') or r.get('SourceText')) for r in reno if r['ID'] == rid))

def show(name, miss, n=8, label=lambda i: item_by[i].get('ItemName_Local', i) if i in item_by else i):
    print(f'\n未覆盖 {name}（{len(miss)}）样例: ' + '、'.join(str(label(i)) for i in miss[:n]))

show('食物', miss_food)
show('植物', miss_plant, label=lambda p: p.get('Name_Local') or p['ID'])
show('猎物', miss_prey)
show('家具', miss_furn, label=lambda f: (furn_by.get(f) or {}).get('Name_Local') or f)
show('装修', miss_reno, label=lambda rid: next((r.get('Remark') or r['ID']) for r in reno if r['ID'] == rid))

# ---------- 缺口探查：家具 ShopPage / 猎物陷阱 / 食物缺口 ----------
print('\n=== 缺口探查 ===')
shoppage = defaultdict(int)
for f in furns:
    sp = f.get('ShopPage')
    shoppage[repr(sp)] += 1
print('Furniture.ShopPage 分布:', dict(shoppage))
try:
    traps = load('Config_Trap'); baits = load('Config_TrapBait')
    print('Trap rows:', len(traps), ' TrapBait rows:', len(baits))
    tcols = [k for k in traps[0].keys() if 'Prey' in k or 'Item' in k or 'prey' in k]
    print('Trap 相关列:', tcols)
    bcols = [k for k in baits[0].keys()]
    print('TrapBait 列:', bcols[:20])
except FileNotFoundError as e:
    print('trap 表缺失:', e)
print('食物缺口样例详情:')
for i in miss_food[:10]:
    it = item_by[i]
    print(f"  #{i} {it.get('ItemName_Local')} Cat={it.get('Category')} Sub={it.get('SubCategory')} Des1={str(it.get('ItemDes1_Local'))[:40]}")
