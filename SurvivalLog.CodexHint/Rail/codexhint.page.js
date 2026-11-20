// CodexHint 页面脚本（Root 帧注入；C# 经 ExecuteJavaScript 带回调重载推送）
// 入口：setData(jsonStr) / setSelection(cat, sid) / probe()
// 渲染：数据/选中变化即时渲染 + 1s 轮询兜底（iframe 重开=新 DOM，轮询最稳）
// 抗崩：全 try-catch，异常 >20 自停本会话
(function () {
  var CH = {
    data: null,          // {plans:{"cat:sid":[{tag,text}]}, degraded:["cat:sid"]}
    sel: null,           // {cat:1, sid:1001}
    lastKey: null,       // 已渲染面板归属 "cat:sid"
    errCount: 0,
    stopped: false
  };

  function fail(e) {
    if (++CH.errCount > 20) { CH.stopped = true; }
  }

  function codexDoc() {
    try {
      var root = document.getElementById('iframe-container');
      if (!root) return null;
      var frames = root.querySelectorAll('iframe');
      for (var i = 0; i < frames.length; i++) {
        var doc = null;
        try { doc = frames[i].contentDocument; } catch (e) { doc = null; }
        if (doc && doc.querySelector && doc.querySelector('.codex-root')) return doc;
      }
    } catch (e) { fail(e); }
    return null;
  }

  function ensureStyle(doc) {
    if (doc.getElementById('codexhint-style')) return;
    var st = doc.createElement('style');
    st.id = 'codexhint-style';
    st.textContent =
      '.codexhint-panel{width:100%;margin:4px 0 8px;text-align:left;}' +
      '.codexhint-title{font-size:11px;color:var(--warm-gold-dim,#c8a256);margin-bottom:6px;letter-spacing:1px;}' +
      '.codexhint-step{display:flex;align-items:flex-start;gap:6px;font-size:12px;line-height:1.7;' +
      'color:var(--text-dim,#b8a89a);margin-bottom:4px;}' +
      '.codexhint-tag{flex:none;font-size:10px;padding:1px 5px;border-radius:3px;margin-top:3px;' +
      'border:1px solid var(--border-color,#6a5a4a);color:var(--warm-gold,#f0b034);opacity:.9;}' +
      '.codexhint-degraded{font-size:12px;color:var(--text-muted,#8a7a6a);line-height:1.7;}';
    doc.head.appendChild(st);
  }

  function buildPanel(doc, steps) {
    var panel = doc.createElement('div');
    panel.className = 'codexhint-panel';
    var title = doc.createElement('div');
    title.className = 'codexhint-title';
    title.textContent = '解锁方案';
    panel.appendChild(title);
    for (var i = 0; i < steps.length; i++) {
      var row = doc.createElement('div');
      row.className = 'codexhint-step';
      var tag = doc.createElement('span');
      tag.className = 'codexhint-tag';
      tag.textContent = steps[i].tag;
      var text = doc.createElement('span');
      text.textContent = steps[i].text;
      row.appendChild(tag); row.appendChild(text);
      panel.appendChild(row);
    }
    return panel;
  }

  var loggedNoDoc = false;
  function render() {
    if (CH.stopped || !CH.sel) return;
    var doc = codexDoc();
    if (!doc) {
      if (!loggedNoDoc && CH.sel) { console.log('[CodexHint] render: 未找到 codex iframe（图鉴未打开属正常）'); loggedNoDoc = true; }
      return;
    }
    var lockedName = doc.querySelector('.detail-name.locked');
    var hint = doc.querySelector('.detail-locked-hint');
    var old = doc.querySelector('.codexhint-panel');
    // 解锁态或官方锁定结构不在场：清旧面板即可（同帧解锁翻转自清）
    if (!lockedName || !hint) {
      if (old && old.parentNode) old.parentNode.removeChild(old);
      CH.lastKey = null;
      return;
    }
    var key = CH.sel.cat + ':' + CH.sel.sid;
    if (old && CH.lastKey === key) return; // 已渲染同条目
    if (old && old.parentNode) old.parentNode.removeChild(old);
    ensureStyle(doc);
    var steps = CH.data && CH.data.plans ? CH.data.plans[key] : null;
    if (!steps || !steps.length) return; // 无方案=不渲染（降级条目保持官方原样，不硬凑占位行）
    hint.parentNode.insertBefore(buildPanel(doc, steps), hint.nextSibling);
    CH.lastKey = key;
    console.log('[CodexHint] render: ' + key + ' 面板已插入');
  }

  CH.setData = function (jsonStr) {
    try {
      CH.data = JSON.parse(jsonStr);
      console.log('[CodexHint] setData: ' + jsonStr.length + 'B plans=' +
        Object.keys(CH.data.plans || {}).length + ' degraded=' + (CH.data.degraded || []).length);
      render();
    } catch (e) { console.log('[CodexHint] setData error: ' + e); fail(e); }
  };
  CH.setSelection = function (cat, sid) {
    try {
      CH.sel = { cat: cat, sid: sid };
      console.log('[CodexHint] setSelection: ' + cat + ':' + sid);
      render();
    } catch (e) { console.log('[CodexHint] setSelection error: ' + e); fail(e); }
  };
  CH.probe = function () { return CH.stopped ? 'stopped' : 'ok'; };

  window.__CODEXHINT__ = CH;
  setInterval(function () { try { render(); } catch (e) { fail(e); } }, 1000);
})();
