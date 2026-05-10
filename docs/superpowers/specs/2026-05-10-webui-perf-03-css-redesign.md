# WebUI 性能优化 — 方向 3: Flat 极简扁平 CSS 重设计

## 问题

当前 CSS 使用 Apple iOS 暗色风格（Puppertino 框架），存在以下性能问题：

- 3 组 `@keyframes` 动画使用 `box-shadow`（触发 paint，非 compositor-only）
- 多处 `transition: all` 导致不必要的属性监听
- `box-shadow` 在面板、开关、滑块等 7+ 处使用（GPU 绘制开销）
- 半透明 `rgba` 玻璃背景增加图层合成成本
- 引用了 Puppertino 框架（额外 CSS 文件）

## 目标

替换为一套极简扁平暗色主题，参考 Linear app / 现代开发者工具风格。核心原则：

1. **纯色** — 无半透明玻璃效果，减少图层合成
2. **无阴影** — 移除全部 `box-shadow`，用细边框和间距制造层次
3. **动画仅用 opacity/transform** — 全部 compositor-only，无重绘
4. **精确 transition** — 无 `transition: all`
5. **CSS containment** — 独立渲染区域，减少回流传播

## 设计

### 颜色系统

```css
:root {
    --bg-primary:   #1c1c1e;   /* 面板底色 */
    --bg-secondary: #2c2c2e;   /* 按钮 / 输入框 / 开关轨道 */
    --bg-tertiary:  #3a3a3c;   /* hover 状态底色 */
    --border:       rgba(255, 255, 255, 0.08); /* 边框 / 分隔线 */
    --text-primary:   #ffffff;
    --text-secondary: rgba(255, 255, 255, 0.55);
    --text-tertiary:  rgba(255, 255, 255, 0.25);
    --accent-green: #30d158;
    --accent-blue:  #0a84ff;
    --accent-red:   #ff453a;
    --accent-orange:#ff9f0a;
    --radius-sm: 6px;
    --radius-md: 12px;
    --radius-pill: 999px;
    --font: -apple-system, system-ui, sans-serif;
}
```

### 关键替换

| 当前 | 替换为 | 收益 |
|------|--------|------|
| `.status-dot` 动画 `box-shadow` | `opacity` 脉冲 | paint → compositor |
| `.status-dot.paused` 动画 `box-shadow` + `opacity` | 仅 `opacity` 脉冲 | 去重绘 |
| `.hk-cell.flash` 动画 `background` + `transform` | 仅 `transform: scale` + `opacity` | 去重绘 |
| `#main-win` 过渡 `width` / `max-height` / `border-radius` | 保留（用户触发，频次低） | 不影响 |
| `transition: all`（6 处） | `transition: background-color, color` | 减少监听属性 |
| `.bar-btn` / `.btn-sm` / `.tab-btn` / `.qt-chip` / `.hk-cell` `box-shadow` | 移除 | 无阴影绘制 |
| `.ios-switch .track:before` `box-shadow` | 移除 | 无阴影绘制 |
| `.set-slider::-webkit-slider-thumb` `box-shadow` | 移除 | 无阴影绘制 |
| `.custom-select-drop` `box-shadow` | 移除 | 无阴影绘制 |
| 半透明 `rgba(28,28,30,0.72)` 玻璃背景 | 纯色 `#1c1c1e` | 减少合成层 |
| `contain` 无 | `#main-win` / `#qt-panel` / `#hk-panel` 加 `contain: layout style paint` | 独立渲染区域 |

### 需删除的文件

| 文件 | 原因 |
|------|------|
| `UI/web/puppertino-bridge.css` | 不再使用 Puppertino |
| `UI/web/puppertino/` | 框架目录，不再引用 |
| `UI/web/vendor/puppertino/` | 同上 |

### 无需改动

- 所有 HTML 文件（class 名称不变，仅 CSS 规则变化）
- `app.js`（样式与 JS 逻辑解耦）
- 方向 1 的 `reportContentSize()` 不受影响

## 修改清单

| 文件 | 改动 |
|------|------|
| `style.css` | 约 70% 规则重写（颜色变量、动画、过渡、contain） |
| `puppertino-bridge.css` | 删除 |
| `UI/web/puppertino/` | 删除目录 |
| `UI/web/vendor/puppertino/` | 删除目录 |

## 参考

- [Linear app](https://linear.app) — 极简扁平暗色 UI
- [VS Code 暗色主题] — 纯色背景 + 细边框分层
