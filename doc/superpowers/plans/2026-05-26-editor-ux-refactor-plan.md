# 编辑器 UX 重构 Implementation Plan

> 基于 UI 规范研究 + VSCode/Node-RED 设计参考

**Goal:** 统一三个编辑器的控件尺寸/间距/字体 + 改进侧栏布局 + 增加折叠面板

**Files:** CSS 3 个 + HTML 3 个 + JS 3 个 = 9 个

---

## 通用 CSS 规范（三个编辑器共享）

### 按钮
| 元素 | 当前 | 改为 |
|---|---|---|
| `.p-btn`, `.btn` (工具栏) | ~22px高, 10px字 | height:32px, padding:4px 12px, font:13px |
| `.btn-sm` | ~18px高 | height:24px, padding:2px 8px, font:12px |
| `.ed-add-btn`, `.ed-del-btn` | ~14px | min-size:22px×22px, font:12px |
| `.qbtn` (快捷添加) | ~18px, padding:2px 6px | height:26px, padding:4px 8px, font:12px |

### 输入/表单
| `.prop-input` | height:~22px, font:11px | height:28px, font:13px, padding:2px 6px |
| `.prop-row` | margin:2px 0 | margin:4px 0 |
| `.prop-label` | font:11px | font:12px, font-weight:600 |
| `textarea.prop-input` | font:11px | font:13px |
| `select.prop-input` | height:~22px | height:28px, font:13px |

### 侧栏/布局
| `.sidebar-label` | font:12px, padding:4px | font:13px, padding:6px 0, letter-spacing:0.5px |
| `.sidebar-section` | margin:4px 0 | margin:6px 0, border-bottom:1px solid var(--bd) |
| `.props-scroll` | — | max-height:calc(100vh - 400px), overflow-y:auto |
| 侧栏宽度 | 不定 | width:280px, min-width:240px |
| toolbar gap | 4px | 6px |

### 触发条件编辑
| `.trigger-cond-item` | padding:6px, margin:4px 0 | padding:8px, margin:6px 0 |
| `.trigger-field` | margin:3px 0, font:12px | margin:5px 0, font:13px |
| `.trigger-field label` | font:12px, color:#aaa | font:12px, color:var(--tx2) |

---

## Task 1: editor.html/js/css — VSCode 折叠侧栏

**Files:** editor.html, editor.js, editor.css

### 1.1 右侧栏改为折叠区块

将属性区域的 5 个平铺区块改为折叠式：

```html
<!-- 轴信息（常驻） -->
<div class="sidebar-section">
  <div class="sidebar-label" onclick="toggleSection('secInfo')">▼ 轴信息</div>
  <div id="secInfo">...已有内容...</div>
</div>

<!-- 基础属性（默认展开） -->
<div class="sidebar-section">
  <div class="sidebar-label" onclick="toggleSection('secBase')">▼ 基础</div>
  <div id="secBase">...DisplayName/ID/Enable/Remark/Tag...</div>
</div>

<!-- 条件/动作（默认展开） -->
<div class="sidebar-section" id="secCondAction">
  <!-- 动态内容 -->
</div>

<!-- 元数据（默认折叠） -->
<div class="sidebar-section">
  <div class="sidebar-label" onclick="toggleSection('secMeta')">▶ 元数据</div>
  <div id="secMeta" style="display:none">...Author/GUID/Job/VarDesc...</div>
</div>

<!-- 暴露变量（默认折叠） -->
<div class="sidebar-section">
  <div class="sidebar-label" onclick="toggleSection('secVars')">▶ 暴露变量</div>
  <div id="secVars" style="display:none">...varList + add button...</div>
</div>
```

### 1.2 JS: toggleSection()

```javascript
function toggleSection(id) {
    var el = document.getElementById(id);
    var hdr = document.querySelector('[onclick="toggleSection(\'' + id + '\')"]');
    if (el.style.display === 'none') {
        el.style.display = '';
        if (hdr) hdr.textContent = '▼ ' + hdr.textContent.substring(2);
    } else {
        el.style.display = 'none';
        if (hdr) hdr.textContent = '▶ ' + hdr.textContent.substring(2);
    }
}
```

### 1.3 CSS: 折叠区块样式

```css
.sidebar-label { cursor: pointer; user-select: none; }
.sidebar-label:hover { color: var(--blue); }
```

---

## Task 2: axflow-editor.html/js/css — Node-RED 画布模式

**Files:** axflow-editor.html, axflow-editor.js, axflow-editor.css

### 2.1 左侧节点面板加折叠

```html
<aside class="palette" id="palettePanel">
  <div class="palette-head" id="paletteToggle" onclick="togglePalette()">◀ 收起</div>
  <div class="palette-list" id="paletteList"></div>
</aside>
```

折叠后缩到 24px 宽，画布自动扩展。

### 2.2 右侧属性面板重构

属性面板只在选中节点时显示内容，否则显示占位文字。

在 renderProps() 中，当 `!selectedDfId` 时保持现状；当选中节点时渲染折叠属性区块。

### 2.3 底部元数据栏

```html
<footer class="footer" id="edFooter">
  <span class="meta-item">作者: <span id="ftAuthor">—</span></span>
  <span class="meta-item">GUID: <span id="ftGuid">—</span></span>
  <span class="meta-item">职业: <span id="ftJob">—</span></span>
  <span style="flex:1"></span>
  <span id="edStatus">就绪</span>
</footer>
```

JS 在 `syncMeta()` 中同步这些字段。

### 2.4 画布 zoom 按钮加大

```css
.canvas-btn { width:36px; height:36px; font-size:14px; line-height:36px; }
```

---

## Task 3: fact-editor.html/js/css — 折叠侧栏

**Files:** fact-editor.html, fact-editor.js, fact-editor.css

### 3.1 右侧栏改为折叠区块

与 editor.html 同样的折叠模式。轴信息常驻，事件属性默认展开，元数据默认折叠。

---

## 通用 CSS 注入

三个 CSS 文件统一加以下规则（覆盖 Puppertino 默认）：

```css
/* === UX 尺寸优化 === */
button, .p-btn, .btn {
    min-height: 30px;
    padding: 4px 12px;
    font-size: 13px;
}
.btn-sm { min-height: 24px; padding: 2px 8px; font-size: 12px; }
.prop-input, input[type="text"], input[type="number"], select {
    height: 28px;
    font-size: 13px;
    padding: 2px 6px;
}
.prop-row { margin: 4px 0; display: flex; align-items: center; gap: 6px; }
.prop-label { font-size: 12px; font-weight: 600; min-width: 60px; }
.ed-add-btn, .ed-del-btn { min-width: 22px; min-height: 22px; font-size: 12px; }
.qbtn { min-height: 26px; padding: 4px 8px; font-size: 12px; }
/* 间距 */
.sidebar-section { margin: 6px 0; }
.toolbar .actions { gap: 6px; }
.trigger-field { margin: 5px 0; }
```
