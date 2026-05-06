# 事实轴编辑器 (Fact Axis Editor)

## TL;DR

> **Quick Summary**: 创建独立的事实轴时间轴编辑器 (`fact-editor.html/css/js`)，采用深色主题垂直时间轴布局，支持最多10个 phase 主轴、事件节点拖拽、小分支横向展开、完整属性编辑和文件操作。
>
> **Deliverables**:
> - `HiAuRo/UI/web/fact-editor.html` — 编辑器页面结构
> - `HiAuRo/UI/web/fact-editor.css` — 深色主题样式 + 时间轴渲染样式
> - `HiAuRo/UI/web/fact-editor.js` — 数据模型 + 渲染 + 交互逻辑
>
> **Estimated Effort**: Large
> **Parallel Execution**: YES — 4 waves
> **Critical Path**: T1 (骨架) → T2 (CSS基础) → T4+T5 (渲染核心) → T8+T9+T10 (交互) → T12+T13 (属性面板) → T15+T16 (文件操作) → F1-F4 (验证)

---

## Context

### Original Request
用户要求基于 `example/事实轴.html` 设计独立的事实轴编辑器，解决4个核心痛点：
1. 主轴线条在深色背景看不见
2. 节点没有简略信息显示
3. 不能选择编辑哪个主轴
4. 小分支没有体现（应从切换分支事件时间开始，相对时间）

### Interview Summary
**Key Discussions**:
- 小分支时间模型：相对于父主轴切换事件时间的相对时间
- 事件可触发处理：切换小分支、切换phase、减伤/治疗需求、输出节奏调整
- 主轴数量：至多10个phase（暂时固定）
- 节点显示：name + time

**Research Findings**:
- 现有 `editor.js` 曾包含事实轴代码（已移除），数据格式已知：`{name, territoryId, author, phases: [{id, name, events, switch}]}``
- 示例 HTML 使用绝对定位 + `top` 进行时间布局
- 项目使用深色主题，CSS 变量命名规范已确立

### Metis Review
**Identified Gaps** (addressed):
- Sub-branch visual layout: 横向从切换事件右侧展开，带独立迷你时间刻度
- Event node coloring: 按 action type 分配颜色
- Drag behavior: 主轴事件垂直拖拽；子分支事件在分支内垂直拖拽
- Cross-phase navigation: 画布顶部水平标签页
- Scroll strategy: 主画布垂直滚动；phase 标签水平滚动（如>10个）

---

## Work Objectives

### Core Objective
创建完整的事实轴时间轴编辑器，支持从 `example/事实轴.html` 参考演化而来的深色主题垂直时间轴，具备事件拖拽、小分支编辑、属性面板和文件操作。

### Concrete Deliverables
- `HiAuRo/UI/web/fact-editor.html`
- `HiAuRo/UI/web/fact-editor.css`
- `HiAuRo/UI/web/fact-editor.js`

### Definition of Done
- [ ] 在浏览器中打开 `fact-editor.html` 可正常显示时间轴
- [ ] 可添加/删除/编辑 phase（主轴）
- [ ] 可添加/删除/编辑事件节点
- [ ] 事件节点可垂直拖拽调整时间
- [ ] 小分支正确显示（相对时间、横向展开）
- [ ] 属性面板可编辑事件所有字段
- [ ] 文件操作（新建/加载/保存/导出）可用
- [ ] 构建通过 `dotnet build HiAuRo/HiAuRo.csproj`

### Must Have
- 深色主题（`#080b12` 背景）
- 最多10个 phase 主轴
- 垂直时间轴（时间从上到下递增）
- 事件节点带 name+time 标签
- 小分支相对时间 + 横向展开
- 事件拖拽调整时间
- 属性面板编辑
- 文件操作

### Must NOT Have (Guardrails)
- 不修改现有 `editor.html` / `editor.css` / `editor.js`
- 不修改 C# 后端代码
- 不使用 vis-timeline 等外部库（纯 CSS+JS 实现）
- 不实现与游戏实时同步
- 不实现多人协作

---

## Verification Strategy

### Test Decision
- **Infrastructure exists**: NO (纯前端 HTML，无单元测试框架)
- **Automated tests**: NO
- **Agent-Executed QA**: ALWAYS — 每个任务包含 Playwright 验证场景

### QA Policy
Every task MUST include agent-executed QA scenarios:
- **Frontend/UI**: Playwright opens `fact-editor.html`, interacts with timeline, asserts DOM state, screenshots
- **Evidence**: `.sisyphus/evidence/task-{N}-{scenario-slug}.png`

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation — 4 parallel tasks):
├── T1: HTML skeleton (fact-editor.html)
├── T2: CSS base styles (dark theme + layout)
├── T3: CSS timeline styles (tracks, nodes, labels, branches)
└── T4: JS data model + constants

Wave 2 (Rendering Core — 4 parallel tasks):
├── T5: Render time scale + phase tracks
├── T6: Render event nodes with labels
├── T7: Render sub-branches (relative time, horizontal)
└── T8: Render property panel skeleton

Wave 3 (Interaction — 4 parallel tasks):
├── T9: Event drag (vertical time adjustment)
├── T10: Context menu (add/delete event)
├── T11: Phase management (add/delete/rename)
└── T12: Property panel binding (edit event fields)

Wave 4 (File Operations + Polish — 3 parallel tasks):
├── T13: File operations (new/load/save/export)
├── T14: Phase tab navigation + keyboard shortcuts
└── T15: Polish (node colors by type, scroll sync, edge cases)

Wave FINAL (Verification — 4 parallel reviews):
├── F1: Plan compliance audit (oracle)
├── F2: Code quality review (unspecified-high)
├── F3: Real manual QA (unspecified-high + playwright)
└── F4: Scope fidelity check (deep)
```

---

## TODOs

- [x] **T1. HTML Skeleton (`fact-editor.html`)**

  **What to do**:
  Create `HiAuRo/UI/web/fact-editor.html` with the complete page structure:
  - `<!DOCTYPE html>` with dark theme meta
  - Top toolbar: fact timeline selector (`<select id="factSelector">`), action buttons (New, Load, Save, Export)
  - Left sidebar: phase list panel (`#phaseList`) + file operations
  - Center canvas: timeline container (`#timelineCanvas`) with time scale ruler (`#timeScale`) + phase tracks container (`#phaseTracks`)
  - Right sidebar: property panel (`#propPanel`) with collapsible sections
  - Context menu for canvas right-click (`#ctxMenu`)
  - Include `puppertino` CSS files (buttons, forms) + `fact-editor.css`
  - Link `fact-editor.js` at end of body

  **Must NOT do**:
  - Do not add inline styles (all styles go in fact-editor.css)
  - Do not add placeholder/ demo content in HTML (all content rendered by JS)

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `frontend-design`
  - Reason: HTML structure + semantic layout for a complex UI

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with T2, T3, T4)
  - **Blocks**: T5, T6, T7, T8
  - **Blocked By**: None

  **References**:
  - `HiAuRo/UI/web/editor.html` — Editor page structure reference (toolbar, sidebar layout)
  - `HiAuRo/UI/web/main.html` — Main panel layout patterns
  - `example/事实轴.html` — Reference for timeline canvas structure

  **Acceptance Criteria**:
  - [ ] File exists at `HiAuRo/UI/web/fact-editor.html`
  - [ ] Opens in browser without console errors
  - [ ] DOM contains all required containers: `#timelineCanvas`, `#timeScale`, `#phaseTracks`, `#propPanel`, `#phaseList`, `#ctxMenu`

  **QA Scenarios**:
  ```
  Scenario: Page loads correctly
    Tool: Playwright
    Steps:
      1. Navigate to file:///E:/HiAuRo/HiAuRo/UI/web/fact-editor.html
      2. Assert no console errors (level: error)
      3. Assert body contains '#timelineCanvas'
      4. Assert body contains '#propPanel'
    Expected Result: Page loads, all containers present, 0 console errors
    Evidence: .sisyphus/evidence/task-1-page-load.png
  ```

  **Commit**: YES
  - Message: `feat(fact-editor): add HTML skeleton`
  - Files: `HiAuRo/UI/web/fact-editor.html`

- [x] **T2. CSS Base Styles (`fact-editor.css` part 1)**

  **What to do**:
  Create base CSS variables and layout styles:
  - CSS variables: `--bg0: #080b12`, `--bg1: #101622`, `--bg2: #151b2b`, `--tx0: #e0e0e0`, `--tx1: #a0aec0`, `--tx2: #64748b`, `--accent: #00d4ff`, `--accent-dim: #0077aa`, `--red: #ff4477`, `--green: #00f0a0`, `--purple: #7e57c2`, `--orange: #ff9f0a`
  - Body: dark background with subtle radial gradients
  - Layout: flex container `.editor-layout` (sidebar 280px + canvas flex:1 + props 320px)
  - Toolbar: sticky top bar with button styles
  - Sidebar panels: dark card style with borders
  - Form inputs: dark background, light border, focus glow
  - Scrollbars: thin dark style

  **Must NOT do**:
  - Do not add timeline-specific styles (tracks, nodes) — that's T3
  - Do not add animation keyframes yet

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `make-interfaces-feel-better`
  - Reason: Dark theme CSS variables and layout system

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1

  **References**:
  - `HiAuRo/UI/web/editor.css` — CSS variable naming convention
  - `example/事实轴.html` — Color scheme reference

  **Acceptance Criteria**:
  - [ ] File exists at `HiAuRo/UI/web/fact-editor.css`
  - [ ] All CSS variables defined
  - [ ] Layout renders correctly (three-column flex)

  **QA Scenarios**:
  ```
  Scenario: CSS loads and layout works
    Tool: Playwright
    Steps:
      1. Navigate to fact-editor.html
      2. Assert computed background-color of body is approximately #080b12
      3. Assert `.editor-layout` has display: flex
      4. Screenshot full page
    Expected Result: Dark theme visible, three-column layout
    Evidence: .sisyphus/evidence/task-2-css-base.png
  ```

  **Commit**: YES — groups with T1

- [x] **T3. CSS Timeline Styles (`fact-editor.css` part 2)**

  **What to do**:
  Add timeline-specific styles to `fact-editor.css`:
  - `.time-scale`: absolute positioned, full height, pointer-events none
  - `.time-line`: horizontal divider lines every 5s (60px height), timestamp label left
  - `.phase-track`: vertical main track column (100px wide), flex column, centered
  - `.track-main-line`: 3px wide vertical line, `#00d4ff` solid, `box-shadow: 0 0 8px #00d4ff, 0 0 16px rgba(0,212,255,0.3)`, with subtle track background `rgba(0,212,255,0.03)` spanning full height
  - `.track-label`: sticky top label, dark bg, cyan border, rounded
  - `.event-node`: 20px circle, positioned absolute on track, color by type:
    - switchBranch: `--purple`
    - switchPhase: `--orange`
    - demand: `--red`
    - skillSuggestion: `--accent`
    - default: `--accent`
  - `.event-label`: positioned right of node, 10px font, dark semi-transparent bg (`rgba(13,17,23,0.85)`), white text, max-width 120px, ellipsis overflow
  - `.event-label-alt`: alternate positioning (left side) for overlap avoidance
  - `.sub-branch`: horizontal container extending right from switch event, different border color, relative time scale mini-ruler
  - `.sub-branch-track`: thin vertical line for sub-branch, different color per branch
  - `.sub-branch-event`: smaller node (16px) for sub-branch events
  - `.drop-indicator`: blue horizontal line for drag target (2px, #00d4ff, glow)
  - Context menu: dark themed, border, shadow
  - Property panel sections: collapsible with header

  **Must NOT do**:
  - Do not add animation styles yet
  - Do not make nodes draggable via CSS (JS handles that)

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `make-interfaces-feel-better`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T1, T2, T4)
  - **Blocked By**: T2 (extends same CSS file)

  **References**:
  - `example/事实轴.html` — Track line, node, label styles
  - `HiAuRo/UI/web/editor.css` — CSS patterns, variable usage

  **Acceptance Criteria**:
  - [ ] All timeline CSS classes defined
  - [ ] Track line visible and glowing on dark background
  - [ ] Event nodes have distinct colors by type
  - [ ] Event labels have proper overflow handling

  **QA Scenarios**:
  ```
  Scenario: Timeline styles render correctly
    Tool: Playwright
    Steps:
      1. Create a test HTML that includes fact-editor.css and creates a track + node
      2. Screenshot
      3. Assert track line is visible (not same color as background)
      4. Assert node has box-shadow glow
    Expected Result: Track line clearly visible with glow effect
    Evidence: .sisyphus/evidence/task-3-timeline-styles.png
  ```

  **Commit**: YES — groups with T1, T2

- [x] **T4. JS Data Model + Constants (`fact-editor.js` part 1)**

  **What to do**:
  Create `HiAuRo/UI/web/fact-editor.js` with:
  - Module-level constants:
    - `TIME_STEP = 5` (seconds per grid line)
    - `LINE_HEIGHT = 60` (pixels per TIME_STEP)
    - `MAX_TIME = 300` (max seconds, 5 minutes)
    - `MAX_PHASES = 10`
    - `COLORS = { switchBranch: '#7e57c2', switchPhase: '#ff9f0a', demand: '#ff4477', skillSuggestion: '#00d4ff', setVariable: '#00f0a0', toggleVariable: '#00f0a0', logMessage: '#64748b' }`
    - `ACTION_TEMPLATES` — templates for each action type (same as old editor.js)
  - State variables:
    - `timelineData = null` — current fact timeline object
    - `currentFile = ''`, `fileHandle = null`
    - `isDirty = false`
    - `selectedEventPath = null` — path like 'p0_ev0' or 'p0_switch_br0_ev0'
    - `currentPhaseIdx = 0`
  - Utility functions:
    - `esc(s)` — HTML escape
    - `formatTime(t)` — format seconds to "M:SS" or "S.s"
    - `newTimeline()` — create empty timeline with 1 default phase
    - `getEventByPath(path)` — resolve event from path string
    - `getParentInfo(path)` — return { container, idx, isSwitchBranch }
    - `isCompositePhase(phase)` — check if phase has switch/branches

  **Must NOT do**:
  - Do not add rendering functions yet (T5-T7)
  - Do not add event handlers yet (T9-T12)

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: [] (pure JS constants and helpers)

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T1, T2, T3)

  **References**:
  - `HiAuRo/UI/web/editor.js` — old fact axis data model (removed but pattern known)
  - `HiAuRo/HiAuRo/FactAxis/sample_timeline.json` — sample data format

  **Acceptance Criteria**:
  - [ ] File exists at `HiAuRo/UI/web/fact-editor.js`
  - [ ] All constants defined
  - [ ] `newTimeline()` returns valid object with phases array
  - [ ] `getEventByPath('p0_ev0')` resolves correctly

  **QA Scenarios**:
  ```
  Scenario: Data model works
    Tool: Bash (node REPL)
    Steps:
      1. node -e "require('./fact-editor.js'); console.log('loaded')"
      2. (Or use browser console to test functions)
    Expected Result: No syntax errors, functions callable
    Evidence: .sisyphus/evidence/task-4-js-model.txt
  ```

  **Commit**: YES — groups with Wave 1

---

## TODOs (Wave 2 — Rendering Core)

- [x] **T5. Render Time Scale + Phase Tracks**

  **What to do**:
  Implement rendering functions in `fact-editor.js`:
  - `renderTimeScale()` — render vertical time ruler from 0s to MAX_TIME in TIME_STEP increments. Each line is a `.time-line` div at `top = t / TIME_STEP * LINE_HEIGHT` px. Label shows time in "M:SS" format.
  - `renderPhaseTracks()` — render all phases from `timelineData.phases`. Each phase becomes a `.phase-track` column. Track has `.track-label` (sticky top) and `.track-main-line` (vertical line spanning full canvas height). Phase tabs at top of canvas for switching current phase.
  - `renderCurrentPhase()` — render only the currently selected phase (or all phases if few). Use `currentPhaseIdx` to determine which phase is active.
  - Canvas container height = `(MAX_TIME / TIME_STEP) * LINE_HEIGHT` px minimum.

  **Must NOT do**:
  - Do not render events yet (T6)
  - Do not render sub-branches yet (T7)

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `frontend-design`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T6, T7, T8)
  - **Blocked By**: T1, T2, T3, T4

  **Acceptance Criteria**:
  - [ ] Time scale renders with 0s, 5s, 10s... labels
  - [ ] Phase track line is visible with glow effect
  - [ ] Phase label sticks to top when scrolling
  - [ ] Switching phase tabs updates displayed track

  **QA Scenarios**:
  ```
  Scenario: Phase track renders
    Tool: Playwright
    Steps:
      1. Open fact-editor.html with test data (1 phase)
      2. Assert '.phase-track' exists
      3. Assert '.track-main-line' has height > 0
      4. Screenshot
    Expected Result: Vertical cyan line visible on dark background
    Evidence: .sisyphus/evidence/task-5-phase-track.png
  ```

  **Commit**: YES
  - Message: `feat(fact-editor): add time scale and phase track rendering`

- [x] **T6. Render Event Nodes with Labels**

  **What to do**:
  Implement `renderEvents()`:
  - For each event in current phase's `events` array:
    - Create `.event-node` div, position absolute at `top = event.time / TIME_STEP * LINE_HEIGHT`
    - Color based on first action's type (fallback to default cyan)
    - Create `.event-label` span positioned right of node with `event.time | event.name`
    - If label would overlap with previous event's label (within 30px), use `.event-label-alt` (positioned left of node)
    - Node has `data-path="p{phaseIdx}_ev{eventIdx}"` for identification
  - Add click handler: select event, highlight node (add `.sel` class), show property panel
  - Add double-click handler: enter inline rename (optional, can skip)

  **Must NOT do**:
  - Do not implement drag yet (T9)
  - Do not render sub-branch events yet (T7)

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `frontend-design`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T5, T7, T8)

  **Acceptance Criteria**:
  - [ ] Event nodes render at correct vertical positions
  - [ ] Labels show `time | name` format
  - [ ] Labels don't overlap (alternating sides)
  - [ ] Clicking node selects it (adds `.sel` class)
  - [ ] Colors match action type

  **QA Scenarios**:
  ```
  Scenario: Event nodes render with labels
    Tool: Playwright
    Steps:
      1. Open fact-editor.html with test data (3 events at 5s, 15s, 25s)
      2. Assert 3 '.event-node' elements exist
      3. Assert first label contains '5s |'
      4. Assert nodes have different colors if actions differ
      5. Screenshot
    Expected Result: 3 nodes visible with correct labels
    Evidence: .sisyphus/evidence/task-6-event-nodes.png
  ```

  **Commit**: YES

- [x] **T7. Render Sub-Branches (Relative Time, Horizontal)**

  **What to do**:
  Implement sub-branch rendering for phases with `switch` data:
  - For a phase with `phase.switch.branches`:
    - Find the switch event in `phase.events` (the event that triggers branch switch)
    - Render each branch as a `.sub-branch` container positioned to the RIGHT of the switch event node
    - Sub-branch container has:
      - Mini header with branch name
      - `.sub-branch-track` vertical line (thinner, different color per branch)
      - `.sub-branch-ruler` mini time scale (0s, 5s, 10s... relative)
    - Render branch events with relative time: `top = event.time / TIME_STEP * LINE_HEIGHT`
    - Branch events use `.sub-branch-event` (16px, smaller than main nodes)
    - Events have `data-path="p{phaseIdx}_switch_br{branchIdx}_ev{eventIdx}"`
    - If multiple branches, they stack horizontally with gap
  - Colors: each branch gets a distinct color from a palette (purple, orange, green, pink, etc.)

  **Must NOT do**:
  - Do not implement branch event drag yet
  - Do not implement branch event editing yet

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `frontend-design`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T5, T6, T8)

  **Acceptance Criteria**:
  - [ ] Sub-branch container renders to the right of switch event
  - [ ] Branch events positioned by relative time
  - [ ] Mini time ruler shows relative seconds (0s, 5s...)
  - [ ] Each branch has distinct color
  - [ ] Branch name visible in header

  **QA Scenarios**:
  ```
  Scenario: Sub-branch renders correctly
    Tool: Playwright
    Steps:
      1. Open fact-editor.html with test data (phase with switch + 2 branches, each with 2 events)
      2. Assert '.sub-branch' exists
      3. Assert first branch event top position > switch event top position (if relative time > 0)
      4. Assert branch name visible in header
      5. Screenshot
    Expected Result: Horizontal sub-branch visible with relative time events
    Evidence: .sisyphus/evidence/task-7-sub-branch.png
  ```

  **Commit**: YES

- [x] **T8. Render Property Panel Skeleton**

  **What to do**:
  Implement `renderProps()` to show property panel for selected event:
  - If no event selected: show hint "点击事件查看属性"
  - If event selected:
    - Section: 基本信息 — inputs for name, id, time (number), duration (number)
    - Section: 同步校准 — startSync/endSync editors (type, abilityIds)
    - Section: 动作列表 — list of actions with type selector and per-type fields:
      - `demand`: 需求减伤%, 需求治疗
      - `skillSuggestion`: skillId, label, priority (high/normal/optional)
      - `setVariable`: variableName, value (true/false)
      - `toggleVariable`: variableName
      - `logMessage`: message
      - `switchPhase`: targetPhase, label
      - `switchBranch`: condition, targetBranch
    - Each action has delete button; section has "+ 添加动作" button
    - Section footer: "删除事件" button
  - Use CSS classes from editor.css for form styling consistency

  **Must NOT do**:
  - Do not implement binding (two-way data sync) yet (T12)
  - Do not implement add/delete actions yet

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `frontend-design`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T5, T6, T7)

  **Acceptance Criteria**:
  - [ ] Property panel renders when event clicked
  - [ ] All sections visible: 基本信息, 同步校准, 动作列表
  - [ ] Action type selector shows all types
  - [ ] Per-type fields show correctly

  **QA Scenarios**:
  ```
  Scenario: Property panel shows event details
    Tool: Playwright
    Steps:
      1. Open fact-editor.html with test data
      2. Click first event node
      3. Assert '#propPanel' contains event name
      4. Assert '#propPanel' contains '动作列表'
      5. Screenshot
    Expected Result: Property panel populated with event data
    Evidence: .sisyphus/evidence/task-8-property-panel.png
  ```

  **Commit**: YES

---

## TODOs (Wave 3 — Interaction)

- [x] **T9. Event Drag (Vertical Time Adjustment)**

  **What to do**:
  Implement manual drag for event nodes (same pattern as editor.js tree drag):
  - `mousedown` on `.event-node` (left button only): record start Y, mark drag active
  - `mousemove` (global): if dragged > 4px, add `.dragging` class to node, show `.drop-indicator` line at snap positions (every TIME_STEP grid line)
  - Snap to nearest TIME_STEP grid: `time = round(y / LINE_HEIGHT) * TIME_STEP`
  - `mouseup` (global): update event.time, call `markDirty()`, re-render
  - Drag state object: `{ active, srcPath, startY, moved }`
  - Prevent click after drag: use `preventClick` flag with 50ms timeout
  - Sub-branch events: same drag logic but within their branch container
  - Safety: clamp time to `>= 0` and `< MAX_TIME`

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `make-interfaces-feel-better`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T10, T11, T12)
  - **Blocked By**: T5, T6, T7

  **Acceptance Criteria**:
  - [ ] Event node can be dragged vertically
  - [ ] Time snaps to 5s grid lines
  - [ ] Drop indicator line shows during drag
  - [ ] After drop, event time updates and re-renders
  - [ ] Click (not drag) still selects event

  **QA Scenarios**:
  ```
  Scenario: Drag event to new time
    Tool: Playwright
    Steps:
      1. Open fact-editor.html with event at 5s
      2. Mouse down on event node
      3. Move mouse down 120px (2 grid lines = 10s)
      4. Mouse up
      5. Assert event node is now at ~15s position
      6. Assert label shows '15s'
    Expected Result: Event moved from 5s to 15s
    Evidence: .sisyphus/evidence/task-9-drag-event.png
  ```

  **Commit**: YES

- [x] **T10. Context Menu (Add/Delete Event)**

  **What to do**:
  Implement context menu for canvas and nodes:
  - **Canvas right-click**: show menu with "添加事件" — adds new event at click time (snapped to grid)
  - **Event node right-click**: show menu with "编辑", "删除", "添加动作"
  - Menu styled with dark theme, positioned at mouse coordinates
  - `contextmenu` event handler: `e.preventDefault(); e.stopPropagation();`
  - Click outside menu closes it
  - Delete: confirm dialog, remove from array, `markDirty()`
  - Add: push new event with default values, `markDirty()`

  **Recommended Agent Profile**:
  - **Category**: `quick`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T9, T11, T12)

  **Acceptance Criteria**:
  - [ ] Right-click canvas shows "添加事件"
  - [ ] Right-click node shows "编辑/删除/添加动作"
  - [ ] Click outside closes menu
  - [ ] Delete removes event and re-renders

  **QA Scenarios**:
  ```
  Scenario: Add event via context menu
    Tool: Playwright
    Steps:
      1. Right-click canvas at ~60px (5s grid)
      2. Click "添加事件"
      3. Assert new event node exists at 5s position
    Expected Result: New event created at clicked time
    Evidence: .sisyphus/evidence/task-10-context-menu.png
  ```

  **Commit**: YES

- [x] **T11. Phase Management (Add/Delete/Rename)**

  **What to do**:
  Implement phase (主轴) CRUD in left sidebar:
  - Phase list panel (`#phaseList`) shows all phases with names
  - Each phase item: name input (inline editable on double-click), delete button (×), active indicator
  - "+ 添加阶段" button at bottom of list
  - Add: push new phase with default name "新阶段", empty events, `markDirty()`
  - Delete: confirm dialog, remove from `phases` array. If deleting current phase, switch to nearest phase.
  - Rename: inline input field, blur/Enter to save
  - Phase tabs at top of canvas: click to switch `currentPhaseIdx`, re-render
  - Enforce MAX_PHASES = 10 limit (disable add button when reached)

  **Must NOT do**:
  - Do not implement phase reordering (drag to reorder)
  - Do not implement phase copy/duplicate

  **Recommended Agent Profile**:
  - **Category**: `quick`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T9, T10, T12)

  **Acceptance Criteria**:
  - [ ] Can add new phase (up to 10)
  - [ ] Can delete phase with confirmation
  - [ ] Can rename phase inline
  - [ ] Phase tabs switch displayed track
  - [ ] Add button disabled at 10 phases

  **QA Scenarios**:
  ```
  Scenario: Add and switch phase
    Tool: Playwright
    Steps:
      1. Click "+ 添加阶段"
      2. Assert new phase appears in list
      3. Click new phase tab
      4. Assert canvas shows new (empty) track
    Expected Result: New phase created and switchable
    Evidence: .sisyphus/evidence/task-11-phase-mgmt.png
  ```

  **Commit**: YES

- [x] **T12. Property Panel Binding (Edit Event Fields)**

  **What to do**:
  Implement two-way data binding for property panel:
  - Basic info inputs: on `change`/`input`, update event object field, call `markDirty()`
  - Sync section: add/remove startSync/endSync, edit abilityIds (comma-separated string converted to number array)
  - Actions section:
    - Type selector (custom dropdown): on change, replace action with template of new type
    - Per-type inputs: on change, update action property
    - Delete action button: splice from array, re-render panel
    - Add action button: push template action, re-render panel
  - Delete event button: confirm, delete, clear selection
  - After any data change: `markDirty()` → re-render timeline + update footer

  **Must NOT do**:
  - Do not implement undo/redo
  - Do not implement real-time validation (simple HTML5 validation only)

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T9, T10, T11)

  **Acceptance Criteria**:
  - [ ] Editing name updates event name immediately
  - [ ] Editing time updates node position
  - [ ] Changing action type updates fields
  - [ ] Adding action appends to list
  - [ ] Deleting action removes from list

  **QA Scenarios**:
  ```
  Scenario: Edit event via property panel
    Tool: Playwright
    Steps:
      1. Click event node
      2. Change name input to "TestEvent"
      3. Blur input
      4. Assert node label contains "TestEvent"
      5. Change time to 20
      6. Assert node moved to 20s position
    Expected Result: Event properties update in real-time
    Evidence: .sisyphus/evidence/task-12-prop-binding.png
  ```

  **Commit**: YES

---

## TODOs (Wave 4 — File Operations + Polish)

- [x] **T13. File Operations (New/Load/Save/Export)**

  **What to do**:
  Implement file operations toolbar buttons:
  - **New**: `newTimeline()` → empty timeline with 1 default phase, clear filename
  - **Load**: File System Access API (`showOpenFilePicker`) or fallback to `<input type="file">`, parse JSON, validate structure, switch to phase 0
  - **Save**: If `fileHandle` exists, write JSON; otherwise call Save As
  - **Save As**: `showSaveFilePicker` or fallback download via Blob + anchor click
  - **Export**: Same as Save As but always prompts for filename
  - JSON stringify with `null, 2` for pretty print
  - Footer status bar shows filename + dirty indicator

  **Must NOT do**:
  - Do not implement auto-save
  - Do not implement cloud sync

  **Recommended Agent Profile**:
  - **Category**: `quick`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T14, T15)
  - **Blocked By**: T4 (data model)

  **Acceptance Criteria**:
  - [ ] New creates empty timeline
  - [ ] Load reads JSON and populates timeline
  - [ ] Save writes JSON to file
  - [ ] Export downloads JSON
  - [ ] Footer shows filename and dirty state

  **QA Scenarios**:
  ```
  Scenario: Export timeline to JSON
    Tool: Playwright
    Steps:
      1. Open fact-editor.html with test data
      2. Click "导出"
      3. Assert download triggered (intercept download event)
    Expected Result: JSON file downloaded
    Evidence: .sisyphus/evidence/task-13-file-ops.png
  ```

  **Commit**: YES

- [x] **T14. Phase Tab Navigation + Keyboard Shortcuts**

  **What to do**:
  - Phase tabs: horizontal scrollable tab bar above canvas, click to switch phase
  - Tab shows phase name + close button (×) for non-last phases
  - Active tab highlighted
  - Keyboard shortcuts:
    - `Ctrl+S`: Save
    - `Delete`: Delete selected event
    - `Escape`: Close context menu / clear selection
  - Add dirty indicator in tab (dot or asterisk)

  **Must NOT do**:
  - Do not implement complex keyboard navigation (arrow keys to move between nodes)

  **Recommended Agent Profile**:
  - **Category**: `quick`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T13, T15)

  **Acceptance Criteria**:
  - [ ] Phase tabs switch phase on click
  - [ ] Ctrl+S triggers save
  - [ ] Delete removes selected event
  - [ ] Escape clears selection

  **QA Scenarios**:
  ```
  Scenario: Keyboard shortcuts work
    Tool: Playwright
    Steps:
      1. Select event node
      2. Press Delete key
      3. Assert event removed
      4. Press Ctrl+S
      5. Assert save triggered
    Expected Result: Keyboard shortcuts functional
    Evidence: .sisyphus/evidence/task-14-shortcuts.png
  ```

  **Commit**: YES

- [ ] **T15. Polish (Node Colors, Scroll Sync, Edge Cases)**

  **What to do**:
  Final polish pass:
  - Node colors: ensure all action types have distinct, visible colors on dark bg
  - Label overlap: improve algorithm — if two labels within 40px vertical, alternate left/right; if still overlapping, hide secondary label
  - Scroll: canvas scrolls vertically; phase labels remain sticky at top
  - Empty state: when no phases or no events, show helpful empty state message
  - Loading state: when loading file, show brief "加载中..." indicator
  - Error handling: if JSON parse fails, show error message in footer
  - Mobile: ensure basic responsiveness (min-width on canvas, horizontal scroll for tracks)
  - Performance: debounce `renderEvents` if called rapidly (e.g., during drag)
  - Accessibility: add `title` attributes to nodes (event name + time)

  **Must NOT do**:
  - Do not implement full responsive design (desktop-first)
  - Do not implement animations (keep it snappy)

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: `make-interfaces-feel-better`

  **Parallelization**:
  - **Can Run In Parallel**: YES (with T13, T14)

  **Acceptance Criteria**:
  - [ ] All node colors visible on dark background
  - [ ] Labels don't visually overlap
  - [ ] Empty state shows when no data
  - [ ] Footer shows error messages
  - [ ] Basic responsiveness works

  **QA Scenarios**:
  ```
  Scenario: Empty state and error handling
    Tool: Playwright
    Steps:
      1. Click "新建" to clear data
      2. Assert empty state visible
      3. Try to load invalid JSON
      4. Assert error message in footer
    Expected Result: Graceful empty state and error handling
    Evidence: .sisyphus/evidence/task-15-polish.png
  ```

  **Commit**: YES

---

### F1. Plan Compliance Audit — `oracle`
Read the plan end-to-end. Verify each deliverable exists. Check evidence files.

### F2. Code Quality Review — `unspecified-high`
Check for `console.log`, empty catches, commented code, unused variables.

### F3. Real Manual QA — `unspecified-high` + `playwright`
Open `fact-editor.html` in browser. Test all QA scenarios from all tasks.

### F4. Scope Fidelity Check — `deep`
Compare actual implementation against plan specs. Check for scope creep.

---

## Commit Strategy

- **Wave 1**: `feat(fact-editor): add skeleton and base styles`
- **Wave 2**: `feat(fact-editor): add timeline rendering`
- **Wave 3**: `feat(fact-editor): add interactions`
- **Wave 4**: `feat(fact-editor): add file ops and polish`

---

## Success Criteria

### Verification Commands
```bash
# Build check
dotnet build HiAuRo/HiAuRo.csproj -nologo

# File existence check
ls HiAuRo/UI/web/fact-editor.html
ls HiAuRo/UI/web/fact-editor.css
ls HiAuRo/UI/web/fact-editor.js
```

### Final Checklist
- [ ] `fact-editor.html` opens without errors
- [ ] Can add/edit/delete phases
- [ ] Can add/edit/delete events
- [ ] Event drag adjusts time
- [ ] Sub-branches render correctly
- [ ] Property panel works
- [ ] File operations work
- [ ] 0 build errors
