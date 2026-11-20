# -*- coding: utf-8 -*-
"""包裹 id 的代码级引用扫描：全 .text 反汇编找立即数，映射方法名（实证发放路径）"""
import json, bisect, sys
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

sys.stdout.reconfigure(encoding='utf-8', errors='replace')
BASE = r'E:/Game/Mod/survivallog-mods/gamekb/2026-11-19_new'
pe = pefile.PE(BASE + r'/binaries/GameAssembly.dll', fast_load=True)
data = pe.__data__
sj = json.load(open(BASE + r'/il2cpp/script.json', encoding='utf-8'))
methods = sorted(m['Address'] for m in sj['ScriptMethod'])
names = {}
for m in sj['ScriptMethod']:
    names.setdefault(m['Address'], m['Name'])

targets = {104, 109, 111, 119, 120, 121, 1004, 1005, 1006, 1007,
           9101, 9102, 9103, 9104, 9105, 9108, 10120,
           24118, 24120, 24121, 30002, 30003, 30006, 30008, 30010, 30022, 30023,
           1001, 1002, 1003}
for s in pe.sections:
    if b'.text' in s.Name:
        rva, raw, size = s.VirtualAddress, s.PointerToRawData, s.Misc_VirtualSize
md = Cs(CS_ARCH_X86, CS_MODE_64)
hits = {}
for ins in md.disasm(bytes(data[raw:raw + size]), rva):
    if ins.mnemonic not in ('mov', 'cmp', 'lea'):
        continue
    for t in targets:
        if hex(t) in ins.op_str:
            i = bisect.bisect_right(methods, ins.address)
            owner = names.get(methods[i - 1], '?') if i else '?'
            short = owner.split('$')[0].replace('GameCore.HotUpdate.', '')
            hits.setdefault(t, set()).add(short.split('(')[0])
for t in sorted(hits):
    print(t, '->', sorted(hits[t])[:4])
print('(未列出的 id = 代码零引用)')
