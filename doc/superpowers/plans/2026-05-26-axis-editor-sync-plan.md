# 轴编辑器同步与增强 Implementation Plan

> 基于 `doc/AXIS_SYSTEM_ARCHITECTURE.md` 分析结果

**Goal:** 同步三个编辑器的功能覆盖，补全缺失的节点类型/属性编辑，增加执行轴编辑器加载事实轴文件的能力

**Architecture:** 纯粹前端改动（JS/HTML/CSS），无 C# 变更

---

## Task 1: axflow-editor 补全缺失节点类型

**Files:**
- Modify: `HiAuRo/UI/web/axflow-editor.js` (NODE_DEFS + TYPE_FULL)

**改动：**

在 `NODE_DEFS` (line 7-17) 加 2 个缺失节点：

```javascript
{ type: 'treePrintNode', label: '调试输出', color: '#868e96', cssClass: 'c-print', composite: false },
{ type: 'treeClearWait', label: '清除等待', color: '#495057', cssClass: 'c-clear', composite: false }
```

在 `TYPE_FULL` (line 19-29) 加映射：

```javascript
treePrintNode:  'HiAuRo.Execution.TreePrintDebugInfoNode, HiAuRo',
treeClearWait:  'HiAuRo.Execution.TreeClearWaitNode, HiAuRo'
```

在 `newNodeDefaults()` (line 1146+ area) 加默认值：

```javascript
if (type === 'treePrintNode') d.Info = '';
if (type === 'treeClearWait') d.OnlyPreNode = true;
```

在 `renderProps()` (line 783+) 加属性渲染：

```javascript
if (type === 'treePrintNode') {
    h += '<div class="prop-section"><div class="prop-head">调试输出</div>';
    h += propRow('内容', 'text', node, 'Info');
    h += '</div>';
}
if (type === 'treeClearWait') {
    h += '<div class="prop-section"><div class="prop-head">清除等待</div>';
    h += propRow('仅前置', 'checkbox', node, 'OnlyPreNode');
    h += '</div>';
}
```

在 `getNodeDefByData()` 添加类型识别。

---

## Task 2: 两个编辑器补全缺失属性

**Files:**
- Modify: `HiAuRo/UI/web/editor.js` (renderProps)
- Modify: `HiAuRo/UI/web/axflow-editor.js` (renderProps)

### 2.1 两个编辑器都加 CondLogicType

**editor.js:**
在 treeCondNode 区块中 `prop('结果取反', ...)` 之前加：
```javascript
h += prop('条件逻辑', 'select', node, 'CondLogicType', 
    [{ value: 0, label: 'And (全部满足)' }, { value: 1, label: 'Or (任一满足)' }]);
```
prop 函数需要支持 select 类型 — 如果已有的 prop 函数只支持 checkbox/number/text，需要扩展。

**axflow-editor.js:**
在 treeCondNode 区块中 `propRow('结果取反', ...)` 之前加：
```javascript
h += '<div class="prop-row"><span class="prop-label">条件逻辑</span>';
h += '<select class="prop-input" id="prop_CondLogicType" data-key="CondLogicType">';
h += '<option value="0" ' + (node.CondLogicType === 1 ? '' : 'selected') + '>And (全部满足)</option>';
h += '<option value="1" ' + (node.CondLogicType === 1 ? 'selected' : '') + '>Or (任一满足)</option>';
h += '</select></div>';
```

### 2.2 editor.js 补 OnlyCheck

在 TreeScriptNode 区块中 `prop('事实轴节点 ID', ...)` 之前加：
```javascript
h += prop('仅检查(不等待)', 'checkbox', node, 'OnlyCheck');
```

### 2.3 axflow-editor 补 FactNodeId

在 TreeScriptNode 区块中 `propRow('仅检查', ...)` 之后加：
```javascript
h += propRow('事实轴节点', 'text', node, 'FactNodeId');
```

---

## Task 3: 执行轴编辑器加载事实轴文件 + FactNodeId 选择器

**Files:**
- Modify: `HiAuRo/UI/web/editor.html`
- Modify: `HiAuRo/UI/web/editor.js`
- Modify: `HiAuRo/UI/web/editor.css`
- Modify: `HiAuRo/UI/web/axflow-editor.html`
- Modify: `HiAuRo/UI/web/axflow-editor.js`
- Modify: `HiAuRo/UI/web/axflow-editor.css`

### 3.1 数据加载

两个编辑器各加：
- 按钮 "加载事实轴"
- 隐藏 `<input type="file">` 用于选择 fact axis JSON
- 加载后解析 FactTimelineData，提取所有可引用节点：
  - 阶段 ID: `phase.id`
  - 事件 ID: `event.id`
  - 分支切换 ID: `switch` (path-based identifier)

```javascript
// 状态变量
var factAxisData = null;   // 加载的事实轴数据
var factNodeTree = [];      // 提取的节点树 [{id, label, path, type}]

// 加载函数
function loadFactAxisFile() {
    var input = document.getElementById('factFileInput');
    input.click();
}
// onchange:
function handleFactAxisLoaded(file) {
    var reader = new FileReader();
    reader.onload = function() {
        factAxisData = JSON.parse(reader.result);
        buildFactNodeTree();
    };
    reader.readAsText(file);
}
```

### 3.2 构建可引用节点树

```javascript
function buildFactNodeTree() {
    factNodeTree = [];
    var phases = factAxisData.phases || [];
    phases.forEach(function(phase) {
        // 阶段本身
        factNodeTree.push({ id: phase.id, label: '▸ ' + phase.name, path: 'phases.' + phase.id, type: 'phase' });
        // 阶段内事件
        (phase.events || []).forEach(function(ev) {
            factNodeTree.push({ id: ev.id, label: '  ◎ ' + ev.name + ' (' + ev.time + 's)', path: 'phases.' + phase.id + '.events.' + ev.id, type: 'event' });
        });
        // 分支
        if (phase.switch && phase.switch.branches) {
            phase.switch.branches.forEach(function(br, i) {
                factNodeTree.push({ id: phase.id + '#branch' + i, label: '  ◇ ' + (br.name || 'Branch ' + i), path: 'phases.' + phase.id + '.switch.branches[' + i + ']', type: 'branch' });
            });
        }
    });
}
```

### 3.3 属性面板中的 FactNodeId 选择器

将目前空文本框的 FactNodeId 改为：

```javascript
// 事实轴节点引用
h += '<div class="ed-prop-section"><div class="ed-prop-head">事实轴节点</div>';
if (factNodeTree.length > 0) {
    h += '<select class="ed-prop-input" id="prop_FactNodeId" data-key="FactNodeId">';
    h += '<option value="">— 未绑定 —</option>';
    factNodeTree.forEach(function(n) {
        h += '<option value="' + esc(n.id) + '" ' + (node.FactNodeId === n.id ? 'selected' : '') + '>' + esc(n.label) + '</option>';
    });
    h += '</select>';
} else {
    h += prop('事实轴节点 ID', 'text', node, 'FactNodeId');
    h += '<div class="prop-hint">加载事实轴文件以选择节点</div>';
}
h += '<button class="btn-sm" style="margin-top:4px" id="btnLoadFactAxis">加载事实轴</button>';
h += '</div>';
```

### 3.4 事件绑定

```javascript
document.getElementById('btnLoadFactAxis').addEventListener('click', loadFactAxisFile);
document.getElementById('factFileInput').addEventListener('change', function(e) {
    handleFactAxisLoaded(this.files[0]);
});
```

### 3.5 HTML 添加

在 editor.html 和 axflow-editor.html 中各添加：
```html
<input type="file" id="factFileInput" accept=".json" style="display:none">
```

---

## Task 4: fact-editor 清理无用 catalog 代码

**Files:**
- Modify: `HiAuRo/UI/web/fact-editor.js`

**改动：**

删除以下死代码（因为 fact-editor 不使用 execution 轴的 trigger cond/action）：
- `localTriggers` 变量声明
- catalog 加载相关的事件绑定和函数
- `getEntryControls`, `findCatalogEntry`, `getAllConditions`, `getAllActions` 函数（如果存在）
- `hiAutoLocalTriggers` localStorage 读写（关于 catalog 的部分，保留编辑器自身状态）

**保留：**
- "加载目录" 按钮 → 改为只做提示，或直接删除
- 所有事实轴相关的事件/动作编辑代码

---

## 验证

```bash
# 无 C# 编译需要（纯前端改动）
# 浏览器打开各编辑器页面，验证：
# 1. axflow-editor 可以创建 PrintNode 和 ClearWait 节点
# 2. 两个编辑器都能编辑 CondLogicType
# 3. editor.js 有 OnlyCheck，axflow-editor 有 FactNodeId
# 4. 两个编辑器都能加载事实轴文件并选择节点
# 5. fact-editor 无明显报错
```

## 提交

每完成一个 Task 单独提交：

```
Task 1: "feat: axflow-editor 补全 PrintNode/ClearWait 节点"
Task 2: "feat: 两个编辑器补全 CondLogicType+OnlyCheck+FactNodeId"
Task 3: "feat: 执行轴编辑器加载事实轴文件+FactNodeId选择器"
Task 4: "chore: fact-editor 清理无用 catalog 代码"
```
