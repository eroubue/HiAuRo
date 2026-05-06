
## 2025-05-06 鈥?fact-editor.css Part 1

### 瀹屾垚鍐呭
- 鍒涘缓浜?`HiAuRo/UI/web/fact-editor.css`锛屼负浜嬪疄杞寸紪杈戝櫒鎻愪緵娣辫壊涓婚鍩虹鏍峰紡銆?- 瀹氫箟浜?`:root` CSS 鑷畾涔夊睘鎬э紝瑕嗙洊鑳屾櫙灞傜骇锛?-bg0/bg1/bg2锛夈€佹枃瀛楀眰绾э紙--tx0/tx1/tx2锛夈€佸己璋冭壊涓庤涔夎壊銆佽竟妗嗕笌瀛椾綋銆?- 瀹炵幇浜嗗叏灞€閲嶇疆銆乥ody 鍩虹鏍峰紡锛堝惈 subtle radial gradient锛夈€佺紪杈戝櫒 flex 甯冨眬銆佸伐鍏锋爮銆佸乏鍙充晶闈㈡澘銆佹椂闂磋酱鐢诲竷銆佽〃鍗曞厓绱犮€佹寜閽€佹粴鍔ㄦ潯銆佸簳閮ㄧ姸鎬佹爮銆佸垎鍖烘爣绛俱€佸崱鐗囦笌鎻愮ず鏂囧瓧銆?
### 璁捐鍐崇瓥
- 棰滆壊鍏ㄩ儴浣跨敤 CSS 鍙橀噺锛屼粎鍦?`:root` 涓‖缂栫爜 hex锛屽悗缁淮鎶ゅ彧闇€鏀逛竴澶勩€?- 鍛藉悕娌跨敤 `editor.css` 鐨?`--bgN` / `--txN` 灞傜骇浣撶郴锛屼繚鎸佽法缂栬緫鍣ㄤ竴鑷存€с€?- 鎸夐挳娣诲姞浜?`.primary` 鍜?`.danger` 璇箟绫伙紝鏂逛究浜嬪疄杞寸紪杈戝櫒涓殑"淇濆瓨"鍜?鍒犻櫎"鎿嶄綔銆?- 婊氬姩鏉￠噰鐢?6px 缁嗘粴鍔ㄦ潯锛宧over 鏃跺彉浜紝绗﹀悎娣辫壊涓婚涔犳儻銆?- body 鑳屾櫙浣跨敤鍙屽眰 `radial-gradient`锛屼笌 `example/浜嬪疄杞?html` 淇濇寔涓€鑷寸殑姘涘洿鎰熴€?
### 绾︽潫閬靛畧
- 鏈姞鍏ヤ换浣曟椂闂磋酱涓撳睘鏍峰紡锛堝 `.phase-track`銆乣.event-node` 绛夛級锛岀暀缁欏悗缁换鍔°€?- 鏈坊鍔?animation keyframes銆?- 鏈慨鏀逛换浣曞凡鏈夋枃浠讹紙editor.css銆乪ditor.html 绛夛級銆?
# 2026-05-06 — fact-editor.js Part 1 (Data Model & Utilities)

## Created
- HiAuRo/UI/web/fact-editor.js — constants, state variables, utility functions, placeholder renders

## Key Decisions
- Used ar for all module-level variables to match existing editor.js conventions
- Path format for events: p{phaseIdx}_ev{eventIdx} (main) / p{phaseIdx}_switch_br{branchIdx}_ev{eventIdx} (branch)
- getEventByPath and getParentInfo parse the path by splitting on _ — the second segment being "switch" disambiguates branch events from main events
- getEventColor reads the first action's 	ype field only for color assignment
- ACTION_TEMPLATES store only the type-specific fields (shared 	ype field is always present)

## Data Model (from sample_timeline.json)
- Timeline: { name, territoryId, author, phases: [...] }
- Phase: { id, name, events: [...], switch: { sync, actions?, branches: [...] } | null }
- Event: { id, name, time, duration?, startSync?, endSync?, actions: [...] }
- Action: { type, ...typeSpecificFields }
- Branch: { name, events: [...], switch? } — branches can nest via their own switch

## Patterns from editor.js
- esc(s) — replace & < > " exactly
- markDirty() — sets flag + calls renderAll() + updateFooter()
- updateFooter() — updates simple textContent on footer element


## 2026-05-06 — fact-editor.css Part 2 (Timeline Styles)

### 完成内容
- 在 act-editor.css 末尾追加了 /* ---- 时间轴渲染样式 ---- */ 区块，新增约 300 行样式。
- 修复布局：#app 设为 display:flex; flex-direction:column; height:100vh;，.editor-layout 追加 lex:1; overflow:hidden; 覆盖原有 height:100vh（通过同选择器后声明覆盖，未修改旧行）。
- 时间刻度：.time-scale 绝对定位铺满画布；.time-line 高 60px，细边框，11px 灰色时间标签左对齐。
- 阶段轨道：.phase-track 宽 100px，flex 列居中；.track-main-line 宽 3px，带发光阴影，min-height 3600px；.track-glare 提供背景辉光；.track-label sticky 置顶。
- 事件节点：.event-node 20px 圆点，中心 8px 白点；.sel 选中态带 currentColor 阴影和白 outline；.dragging 半透明 + grabbing 光标。
- 事件标签：.event-label 右侧展开（margin-left:16px），.event-label-alt 左侧展开（margin-left:-136px），均限制 120px 宽并 ellipsis。

## 2026-05-06 - fact-editor.css Part 2 (Timeline Styles)

### 完成内容
- 在 act-editor.css 末尾追加了 /* ---- 时间轴渲染样式 ---- */ 区块，新增约 300 行样式。
- 修复布局：#app 设为 display:flex; flex-direction:column; height:100vh;，.editor-layout 追加 lex:1; overflow:hidden; 覆盖原有 height:100vh（通过同选择器后声明覆盖，未修改旧行）。
- 时间刻度：.time-scale 绝对定位铺满画布；.time-line 高 60px，细边框，11px 灰色时间标签左对齐。
- 阶段轨道：.phase-track 宽 100px，flex 列居中；.track-main-line 宽 3px，带发光阴影，min-height 3600px；.track-glare 提供背景辉光；.track-label sticky 置顶。
- 事件节点：.event-node 20px 圆点，中心 8px 白点；.sel 选中态带 currentColor 阴影和白 outline；.dragging 半透明 + grabbing 光标。
- 事件标签：.event-label 右侧展开（margin-left:16px），.event-label-alt 左侧展开（margin-left:-136px），均限制 120px 宽并 ellipsis。
- 子分支：.sub-branch 绝对定位容器；.sub-branch-track 2px 细线；.sub-branch-event 16px 小节点配 6px 白点；.sub-branch-label 与 .sub-branch-ruler 提供标签和迷你刻度。
- 交互：.drop-indicator 拖放指示线；.context-menu 固定定位右键菜单，含 .ctx-item、.ctx-sep、.ctx-danger。
- 属性面板：.prop-section、.prop-section-header、.prop-row、.prop-label、.prop-input。
- 动作列表：.action-item、.action-item-header。
- 工具类：.hide 统一为 display:none !important。

### 设计决策
- 全部新样式使用已有 CSS 变量（--accent、--bg1、--tx2 等），无新增硬编码色值（除 rgba 透明度和 #fff 外）。
- .editor-layout 采用追加同选择器覆盖策略，符合"只 append、不修改旧行" 的约束。
- 子分支刻度 .tick 高度取 30px（主刻度 60px 的一半），保持视觉层级。

### 约束遵守
- 未修改 lines 1-263 的任何现有规则。
- 未添加 JavaScript、animation keyframes 或超出指定范围的 transition。

## 2026-05-06 - renderEvents() implemented (T5)

### Implementation
- Replaced empty stub at line 208 with full `renderEvents()` function (lines 208-290)
- Clears old `.event-node`, `.event-label`, `.event-label-alt` via `querySelectorAll` + `remove()` before re-rendering
- Creates event nodes as `<div class="event-node">` with absolute positioning, `data-path`, background color and box-shadow glow from `getEventColor()`
- Creates labels as `<span>` with textContent `${formatTime(ev.time)} | ${ev.name}` 
- Anti-overlap logic: tracks previous event's top position and label side, flips between `.event-label` (right) and `.event-label-alt` (left) when vertical distance < 30px
- Click handler: IIFE-closure captures path and node reference, sets `selectedEventPath`, toggles `.sel` with remove-then-add pattern, calls `renderProps()`
- Empty events early-return after clearing (calls `updateFooter()` before return)
- Selected event state persists across re-renders (checks `selectedEventPath === node.dataset.path`)
- Uses `createElement` + `appendChild` exclusively (no innerHTML string building)
- `textContent` used for labels — auto-escapes, no need for explicit `esc()`

### Pattern Notes
- `.event-label` CSS: `left:50%; margin-left:16px` — positions label to right of centered node
- `.event-label-alt` CSS: `left:50%; margin-left:-136px` — positions label to left (100px track ÷ 2 = 50px center, -136px offset)
- All DOM cleanup scoped to current phase track via `querySelector` with `data-phase-idx` attribute

## 2026-05-06 - renderProps() implemented (T10)

### Implementation
- Replaced `function renderProps() {}` stub at line 292 with full property panel rendering (lines 295-456)
- Uses `document.getElementById('propPanel')` for panel container, clears with `panel.innerHTML = ''`
- **Null path guard**: shows `<div class="hint">点击事件查看属性</div>` when `selectedEventPath` is null
- **Event not found guard**: shows `<div class="hint">事件未找到</div>` when `getEventByPath` returns null
- **基本信息 section**: 4 rows — 名称 (text), ID (readonly), 时间(s) (number, step=0.1), 持续(s) (number, step=0.1)
- **同步校准 section**: renders existing startSync/endSync with type dropdown (startsUsing/ability/inCombat), abilityIds comma-separated input, "移除" danger button. Shows "+ 添加" buttons when sync not present.
- **动作列表 section**: For each action, renders type dropdown with Chinese names, then per-type fields. Each action has "×" danger delete button. "+ 添加动作" button at bottom.
- **删除事件**: full-width `btn danger` button
- HTML built as string (`var html = ''`) then set via `innerHTML` — simpler than createElement for this complex nested structure
- All values escaped via `esc()` when going into HTML attributes
- No change handlers implemented (deferred to T12) — just renders static HTML with correct `selected` attributes and `value` attributes

### CSS Classes Used
- `.prop-section`, `.prop-section-header`, `.prop-row`, `.prop-label`, `.prop-input` — for property panel layout
- `.action-item` — for each action card
- `.btn`, `.btn.danger` — for buttons
- `.hint` — for placeholder text

### Per-Action-Type Fields Rendered
- demand: 减伤% (number), 治疗 (number)
- skillSuggestion: 技能ID (number), 名称 (text), 优先级 dropdown (high/normal/optional)
- setVariable: 变量名 (text), 值 dropdown (true/false)
- toggleVariable: 变量名 (text)
- logMessage: 消息 (text)
- switchPhase: 目标阶段 (text), 标签 (text)
- switchBranch: 条件变量 (text), 目标分支 (text)

## 2026-05-06 - renderAllSubBranches() implemented (T8 part)

### Implementation
- Created `renderAllSubBranches()` function (lines 295-419) — renders sub-branches for current phase
- Called from `renderEvents()` at line 290, after main event loop and before `updateFooter()`
- Old sub-branch cleanup: `track.querySelectorAll('.sub-branch')` → `remove()` in a loop
- Branch origin: `top = switchEvent.time / TIME_STEP * LINE_HEIGHT` px
- Switch event lookup: iterates phase.events to find action type 'switchBranch'; falls back to last event if not found
- Returns early if phase has no events (can't position branch origin)
- Container position: `left = 120 + brIdx * 130` px — leaves 100px for phase track + 20px gap + 130px per branch
- Branch colors: `BRANCH_COLORS[brIdx % BRANCH_COLORS.length]`
- Each sub-branch contains: label (.sub-branch-label), track line (.sub-branch-track), ruler (.sub-branch-ruler with .tick marks), and events (.sub-branch-event + .event-label)
- Event dataset.path format: `p{phaseIdx}_switch_br{branchIdx}_ev{eventIdx}` — matches `getEventByPath()` parsing logic
- Click handler clears both .sub-branch-event and .event-node .sel before setting selection
- Labels use inline styles for positioning (left:50%, marginLeft:16px, transform:translateY(-50%)) to match .event-label CSS
- Branch events use `node.style.boxShadow = '0 0 8px ' + brColor` for glow effect
- Re-selected events restore `.sel` class on re-render via `selectedEventPath === path` check

### Patterns
- All DOM created via `createElement` + `appendChild` (no innerHTML)
- `textContent` for labels (auto-escapes)
- IIFE closure for click handlers (captures path + element)
- Same DOM query pattern as `renderEvents()`: scoped to current phase track via `data-phase-idx`
