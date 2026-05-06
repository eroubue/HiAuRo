// ============================================================
// HiAuRo 事实轴编辑器 — 数据模型与工具函数
// ============================================================

// ==================== 常量 ====================

var TIME_STEP = 5;          // 每格秒数
var LINE_HEIGHT = 60;       // 每 TIME_STEP 像素高度
var MAX_TIME = 300;         // 最大秒数 (5分钟)
var MAX_PHASES = 10;        // 阶段数硬上限

var EVENT_COLORS = {
    switchBranch:     '#7e57c2',  // 紫色 - 分支切换
    switchPhase:      '#ff9f0a',  // 橙色 - 阶段切换
    demand:           '#ff4477',  // 红色 - 需求
    skillSuggestion:  '#00d4ff',  // 青色 - 技能建议
    setVariable:      '#00f0a0',  // 绿色 - 设置变量
    toggleVariable:   '#00f0a0',  // 绿色 - 切换变量
    logMessage:       '#64748b',  // 灰色 - 日志
    default:          '#00d4ff'   // 青色 - 默认
};

var BRANCH_COLORS = [
    '#7e57c2', '#ff9f0a', '#00f0a0', '#ff4477', '#00d4ff',
    '#f0d000', '#ff6b9d', '#4ecdc4', '#95e1d3', '#f38181'
];

// ==================== 动作模板 ====================

var ACTION_TEMPLATES = {
    demand:           { type: 'demand', '需求减伤': 0, '需求治疗': 0 },
    skillSuggestion:  { type: 'skillSuggestion', skillId: 0, label: '', priority: 'normal' },
    setVariable:      { type: 'setVariable', variableName: '', value: true },
    toggleVariable:   { type: 'toggleVariable', variableName: '' },
    logMessage:       { type: 'logMessage', message: '' },
    switchPhase:      { type: 'switchPhase', targetPhase: '', label: '' },
    switchBranch:     { type: 'switchBranch', condition: '', targetBranch: '' }
};

// ==================== 状态变量 ====================

var timelineData = null;       // 当前事实轴数据 { name, territoryId, author, phases:[...] }
var currentFile = '';          // 当前文件名
var fileHandle = null;         // FileSystemFileHandle
var isDirty = false;           // 未保存修改标记
var selectedEventPath = null;  // 选中事件路径 (如 'p0_ev0')
var currentPhaseIdx = 0;       // 当前编辑的阶段索引

// ==================== 工具函数 ====================

function esc(s) {
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function formatTime(t) {
    t = Number(t) || 0;
    if (t < 60) return t.toFixed(1) + 's';
    var m = Math.floor(t / 60);
    var s = Math.round(t % 60);
    return m + ':' + (s < 10 ? '0' : '') + s;
}

function newTimeline() {
    return {
        name: '新事实轴',
        territoryId: 0,
        author: '',
        phases: [
            { id: 'p1', name: '阶段1', events: [], switch: null }
        ]
    };
}

function getPhase(phaseIdx) {
    if (!timelineData || !timelineData.phases) return null;
    return timelineData.phases[phaseIdx] || null;
}

function getEventByPath(path) {
    if (!path || !timelineData) return null;
    var parts = path.split('_');
    if (parts.length < 2) return null;
    var phaseIdx = parseInt(parts[0].substring(1));
    if (isNaN(phaseIdx)) return null;
    var phase = getPhase(phaseIdx);
    if (!phase) return null;

    if (parts[1] === 'switch' && parts.length === 4) {
        // p{idx}_switch_br{idx}_ev{idx}
        var brIdx = parseInt(parts[2].substring(2));
        var evIdx = parseInt(parts[3].substring(2));
        if (isNaN(brIdx) || isNaN(evIdx)) return null;
        if (!phase.switch || !phase.switch.branches) return null;
        var branch = phase.switch.branches[brIdx];
        if (!branch || !branch.events) return null;
        return branch.events[evIdx] || null;
    } else if (parts.length === 2) {
        // p{idx}_ev{idx}
        var evIdx = parseInt(parts[1].substring(2));
        if (isNaN(evIdx)) return null;
        return phase.events[evIdx] || null;
    }
    return null;
}

function getParentInfo(path) {
    if (!path || !timelineData) return null;
    var parts = path.split('_');
    if (parts.length < 2) return null;
    var phaseIdx = parseInt(parts[0].substring(1));
    if (isNaN(phaseIdx)) return null;
    var phase = getPhase(phaseIdx);
    if (!phase) return null;

    if (parts[1] === 'switch' && parts.length === 4) {
        // p{idx}_switch_br{idx}_ev{idx}
        var brIdx = parseInt(parts[2].substring(2));
        var evIdx = parseInt(parts[3].substring(2));
        if (isNaN(brIdx) || isNaN(evIdx)) return null;
        if (!phase.switch || !phase.switch.branches) return null;
        var branch = phase.switch.branches[brIdx];
        if (!branch || !branch.events) return null;
        return { container: branch.events, idx: evIdx, isSubBranch: true };
    } else if (parts.length === 2) {
        // p{idx}_ev{idx}
        var evIdx = parseInt(parts[1].substring(2));
        if (isNaN(evIdx)) return null;
        return { container: phase.events, idx: evIdx, isSubBranch: false };
    }
    return null;
}

function getEventColor(ev) {
    if (!ev || !ev.actions || ev.actions.length === 0) return EVENT_COLORS.default;
    var type = ev.actions[0].type;
    return EVENT_COLORS[type] || EVENT_COLORS.default;
}

function markDirty() {
    isDirty = true;
    renderAll();
    updateFooter();
}

// ==================== 渲染 (占位 — T5~T8 实现) ====================

function renderAll() { renderTimeScale(); renderPhaseTracks(); renderEvents(); renderProps(); }

function renderTimeScale() {
    var el = document.getElementById('timeScale');
    if (!el) return;
    el.innerHTML = '';
    for (var t = 0; t <= MAX_TIME; t += TIME_STEP) {
        var div = document.createElement('div');
        div.className = 'time-line';
        div.style.position = 'absolute';
        div.style.left = '0';
        div.style.right = '0';
        div.style.top = (t / TIME_STEP * LINE_HEIGHT) + 'px';
        div.textContent = formatTime(t);
        el.appendChild(div);
    }
}

function renderPhaseTracks() {
    var el = document.getElementById('phaseTracks');
    if (!el) return;

    if (!timelineData || !timelineData.phases || timelineData.phases.length === 0) {
        el.innerHTML = '<div class="hint">暂无阶段数据</div>';
        return;
    }

    el.innerHTML = '';
    // 横向 flex 容器，>3 阶段时滚动
    el.style.display = 'flex';
    el.style.overflowX = 'auto';
    el.style.height = '100%';

    for (var i = 0; i < timelineData.phases.length; i++) {
        var phase = timelineData.phases[i];
        var track = document.createElement('div');
        track.className = 'phase-track';
        track.setAttribute('data-phase-idx', i);

        var label = document.createElement('div');
        label.className = 'track-label';
        label.textContent = phase.name; // textContent 自带转义

        var glare = document.createElement('div');
        glare.className = 'track-glare';

        var mainLine = document.createElement('div');
        mainLine.className = 'track-main-line';
        if (phase.events && phase.events.length > 0) {
            mainLine.style.background = getEventColor(phase.events[0]);
        } else {
            mainLine.style.background = 'var(--accent)';
        }

        track.appendChild(label);
        track.appendChild(glare);
        track.appendChild(mainLine);
        el.appendChild(track);
    }
}

function renderEvents() {
    // 找到当前阶段的轨道容器
    var track = document.querySelector('.phase-track[data-phase-idx="' + currentPhaseIdx + '"]');
    if (!track) return;

    // 清除旧的事件节点和标签
    var oldNodes = track.querySelectorAll('.event-node, .event-label, .event-label-alt');
    for (var n = 0; n < oldNodes.length; n++) {
        oldNodes[n].remove();
    }

    var phase = getPhase(currentPhaseIdx);
    if (!phase || !phase.events || phase.events.length === 0) {
        updateFooter();
        return;
    }

    var events = phase.events;
    var prevTop = -999;      // 上一个事件 top 位置
    var prevSide = 'right';  // 上一个标签使用哪一侧

    for (var i = 0; i < events.length; i++) {
        var ev = events[i];
        var top = ev.time / TIME_STEP * LINE_HEIGHT;

        // ---- 事件节点 ----
        var node = document.createElement('div');
        node.className = 'event-node';
        node.style.top = top + 'px';
        node.dataset.path = 'p' + currentPhaseIdx + '_ev' + i;

        var color = getEventColor(ev);
        node.style.background = color;
        node.style.boxShadow = '0 0 8px ' + color;

        // 已选中的恢复高亮
        if (selectedEventPath === node.dataset.path) {
            node.classList.add('sel');
        }

        // 点击选中
        (function(path, el) {
            el.addEventListener('click', function(e) {
                e.stopPropagation();
                selectedEventPath = path;

                // 清除所有节点的 .sel
                var allNodes = track.querySelectorAll('.event-node');
                for (var a = 0; a < allNodes.length; a++) {
                    allNodes[a].classList.remove('sel');
                }
                el.classList.add('sel');

                renderProps();
            });
        })(node.dataset.path, node);

        // ---- 事件标签 ----
        var label = document.createElement('span');
        label.style.top = top + 'px';
        label.textContent = formatTime(ev.time) + ' | ' + ev.name;

        // 防重叠：与上一个标签垂直距离 < 30px 则交替换侧
        var distance = Math.abs(top - prevTop);
        if (distance < 30 && prevSide === 'right') {
            label.className = 'event-label-alt';
            prevSide = 'left';
        } else if (distance < 30 && prevSide === 'left') {
            label.className = 'event-label';
            prevSide = 'right';
        } else {
            label.className = 'event-label';
            prevSide = 'right';
        }

        prevTop = top;

        track.appendChild(node);
        track.appendChild(label);
    }

    // 渲染子分支
    renderAllSubBranches();

    updateFooter();
}

// 渲染当前阶段的所有子分支
function renderAllSubBranches() {
    var track = document.querySelector('.phase-track[data-phase-idx="' + currentPhaseIdx + '"]');
    if (!track) return;

    // 清除旧的子分支元素
    var oldBranches = track.querySelectorAll('.sub-branch');
    for (var b = 0; b < oldBranches.length; b++) {
        oldBranches[b].remove();
    }

    var phase = getPhase(currentPhaseIdx);
    if (!phase || !phase.switch || !phase.switch.branches || phase.switch.branches.length === 0) return;

    // 找到 switch 事件（action type 为 switchBranch），否则用最后一个事件
    var events = phase.events || [];
    var switchEvent = null;
    for (var i = 0; i < events.length; i++) {
        if (events[i].actions && events[i].actions.length > 0 && events[i].actions[0].type === 'switchBranch') {
            switchEvent = events[i];
            break;
        }
    }
    if (!switchEvent && events.length > 0) {
        switchEvent = events[events.length - 1];
    }
    if (!events.length) return; // 无事件，无法定位分支原点

    var branchOriginTop = switchEvent ? (switchEvent.time / TIME_STEP * LINE_HEIGHT) : 0;

    var branches = phase.switch.branches;
    for (var brIdx = 0; brIdx < branches.length; brIdx++) {
        var branch = branches[brIdx];
        var brColor = BRANCH_COLORS[brIdx % BRANCH_COLORS.length];

        // ---- 容器 ----
        var container = document.createElement('div');
        container.className = 'sub-branch';
        container.setAttribute('data-branch-idx', brIdx);
        container.setAttribute('data-phase-idx', currentPhaseIdx);
        container.style.top = branchOriginTop + 'px';
        container.style.left = (120 + brIdx * 130) + 'px';

        // ---- 分支标签 ----
        var label = document.createElement('div');
        label.className = 'sub-branch-label';
        label.textContent = esc(branch.name);
        container.appendChild(label);

        // ---- 分支轨道线 ----
        var trackLine = document.createElement('div');
        trackLine.className = 'sub-branch-track';
        trackLine.style.background = brColor;
        container.appendChild(trackLine);

        // ---- 迷你时间刻度（相对时间）----
        var ruler = document.createElement('div');
        ruler.className = 'sub-branch-ruler';
        for (var t = 0; t <= MAX_TIME; t += TIME_STEP) {
            var tick = document.createElement('div');
            tick.className = 'tick';
            tick.style.position = 'absolute';
            tick.style.left = '0';
            tick.style.right = '0';
            tick.style.top = (t / TIME_STEP * LINE_HEIGHT) + 'px';
            tick.textContent = formatTime(t);
            ruler.appendChild(tick);
        }
        container.appendChild(ruler);

        // ---- 分支事件 ----
        for (var evIdx = 0; evIdx < branch.events.length; evIdx++) {
            var ev = branch.events[evIdx];
            var evTop = ev.time / TIME_STEP * LINE_HEIGHT;
            var path = 'p' + currentPhaseIdx + '_switch_br' + brIdx + '_ev' + evIdx;

            var node = document.createElement('div');
            node.className = 'sub-branch-event';
            node.style.top = evTop + 'px';
            node.dataset.path = path;
            node.style.background = brColor;
            node.style.boxShadow = '0 0 8px ' + brColor;

            if (selectedEventPath === path) {
                node.classList.add('sel');
            }

            // 点击选中
            (function(p, el) {
                el.addEventListener('click', function(e) {
                    e.stopPropagation();
                    selectedEventPath = p;

                    // 清除所有子分支事件的 .sel
                    var allSubNodes = track.querySelectorAll('.sub-branch-event');
                    for (var a = 0; a < allSubNodes.length; a++) {
                        allSubNodes[a].classList.remove('sel');
                    }
                    // 清除主事件节点的 .sel
                    var allMainNodes = track.querySelectorAll('.event-node');
                    for (var m = 0; m < allMainNodes.length; m++) {
                        allMainNodes[m].classList.remove('sel');
                    }

                    el.classList.add('sel');
                    renderProps();
                });
            })(path, node);

            // 事件标签
            var evLabel = document.createElement('span');
            evLabel.className = 'event-label';
            evLabel.style.top = evTop + 'px';
            evLabel.textContent = formatTime(ev.time) + ' | ' + ev.name;
            evLabel.style.left = '50%';
            evLabel.style.marginLeft = '16px';
            evLabel.style.transform = 'translateY(-50%)';

            container.appendChild(node);
            container.appendChild(evLabel);
        }

        track.appendChild(container);
    }
}

function renderProps() {
    var panel = document.getElementById('propPanel');
    if (!panel) return;
    panel.innerHTML = '';

    if (!selectedEventPath) {
        panel.innerHTML = '<div class="hint">点击事件查看属性</div>';
        return;
    }

    var ev = getEventByPath(selectedEventPath);
    if (!ev) {
        panel.innerHTML = '<div class="hint">事件未找到</div>';
        return;
    }

    var html = '';

    // ==================== 基本信息 ====================
    html += '<div class="prop-section">';
    html += '<div class="prop-section-header">基本信息</div>';

    // 名称
    html += '<div class="prop-row">';
    html += '<span class="prop-label">名称</span>';
    html += '<input class="prop-input" type="text" value="' + esc(ev.name || '') + '">';
    html += '</div>';

    // ID (只读)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">ID</span>';
    html += '<input class="prop-input" type="text" value="' + esc(ev.id || '') + '" readonly>';
    html += '</div>';

    // 时间(s)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">时间(s)</span>';
    html += '<input class="prop-input" type="number" value="' + (ev.time || 0) + '" step="0.1">';
    html += '</div>';

    // 持续(s)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">持续(s)</span>';
    html += '<input class="prop-input" type="number" value="' + (ev.duration || 0) + '" step="0.1">';
    html += '</div>';

    html += '</div>'; // end 基本信息

    // ==================== 同步校准 ====================
    html += '<div class="prop-section">';
    html += '<div class="prop-section-header">同步校准</div>';

    // 开始同步
    if (ev.startSync) {
        html += '<div style="margin-bottom:8px;">';
        html += '<span style="display:inline-block;width:60px;font-size:11px;color:var(--tx1);">开始同步</span>';
        html += '<select class="prop-input" style="width:auto;">';
        html += '<option value="startsUsing"' + (ev.startSync.type === 'startsUsing' ? ' selected' : '') + '>startsUsing</option>';
        html += '<option value="ability"' + (ev.startSync.type === 'ability' ? ' selected' : '') + '>ability</option>';
        html += '<option value="inCombat"' + (ev.startSync.type === 'inCombat' ? ' selected' : '') + '>inCombat</option>';
        html += '</select>';
        html += '</div>';
        html += '<div class="prop-row">';
        html += '<span class="prop-label">技能ID</span>';
        html += '<input class="prop-input" type="text" value="' + esc((ev.startSync.abilityIds || []).join(',')) + '" placeholder="逗号分隔">';
        html += '</div>';
        html += '<button class="btn danger" style="margin-bottom:8px;">移除</button>';
    } else {
        html += '<button class="btn" style="margin-bottom:4px;">+ 添加开始同步</button>';
    }

    // 结束同步
    if (ev.endSync) {
        html += '<div style="margin-bottom:8px;">';
        html += '<span style="display:inline-block;width:60px;font-size:11px;color:var(--tx1);">结束同步</span>';
        html += '<select class="prop-input" style="width:auto;">';
        html += '<option value="startsUsing"' + (ev.endSync.type === 'startsUsing' ? ' selected' : '') + '>startsUsing</option>';
        html += '<option value="ability"' + (ev.endSync.type === 'ability' ? ' selected' : '') + '>ability</option>';
        html += '<option value="inCombat"' + (ev.endSync.type === 'inCombat' ? ' selected' : '') + '>inCombat</option>';
        html += '</select>';
        html += '</div>';
        html += '<div class="prop-row">';
        html += '<span class="prop-label">技能ID</span>';
        html += '<input class="prop-input" type="text" value="' + esc((ev.endSync.abilityIds || []).join(',')) + '" placeholder="逗号分隔">';
        html += '</div>';
        html += '<button class="btn danger" style="margin-bottom:8px;">移除</button>';
    } else {
        html += '<button class="btn">+ 添加结束同步</button>';
    }

    html += '</div>'; // end 同步校准

    // ==================== 动作列表 ====================
    html += '<div class="prop-section">';
    html += '<div class="prop-section-header">动作</div>';

    var actions = ev.actions || [];
    for (var i = 0; i < actions.length; i++) {
        var a = actions[i];
        html += '<div class="action-item">';

        // 动作类型下拉
        html += '<div class="prop-row">';
        html += '<span class="prop-label">类型</span>';
        html += '<select class="prop-input">';
        html += '<option value="demand"' + (a.type === 'demand' ? ' selected' : '') + '>需求</option>';
        html += '<option value="skillSuggestion"' + (a.type === 'skillSuggestion' ? ' selected' : '') + '>技能建议</option>';
        html += '<option value="setVariable"' + (a.type === 'setVariable' ? ' selected' : '') + '>设置变量</option>';
        html += '<option value="toggleVariable"' + (a.type === 'toggleVariable' ? ' selected' : '') + '>切换变量</option>';
        html += '<option value="logMessage"' + (a.type === 'logMessage' ? ' selected' : '') + '>日志</option>';
        html += '<option value="switchPhase"' + (a.type === 'switchPhase' ? ' selected' : '') + '>切换阶段</option>';
        html += '<option value="switchBranch"' + (a.type === 'switchBranch' ? ' selected' : '') + '>切换分支</option>';
        html += '</select>';
        html += '</div>';

        // 按类型渲染字段
        if (a.type === 'demand') {
            html += '<div class="prop-row"><span class="prop-label">减伤%</span><input class="prop-input" type="number" value="' + (a['需求减伤'] || 0) + '"></div>';
            html += '<div class="prop-row"><span class="prop-label">治疗</span><input class="prop-input" type="number" value="' + (a['需求治疗'] || 0) + '"></div>';
        } else if (a.type === 'skillSuggestion') {
            html += '<div class="prop-row"><span class="prop-label">技能ID</span><input class="prop-input" type="number" value="' + (a.skillId || 0) + '"></div>';
            html += '<div class="prop-row"><span class="prop-label">名称</span><input class="prop-input" type="text" value="' + esc(a.label || '') + '"></div>';
            html += '<div class="prop-row"><span class="prop-label">优先级</span>';
            html += '<select class="prop-input">';
            html += '<option value="high"' + (a.priority === 'high' ? ' selected' : '') + '>high</option>';
            html += '<option value="normal"' + (a.priority === 'normal' ? ' selected' : '') + '>normal</option>';
            html += '<option value="optional"' + (a.priority === 'optional' ? ' selected' : '') + '>optional</option>';
            html += '</select></div>';
        } else if (a.type === 'setVariable') {
            html += '<div class="prop-row"><span class="prop-label">变量名</span><input class="prop-input" type="text" value="' + esc(a.variableName || '') + '"></div>';
            html += '<div class="prop-row"><span class="prop-label">值</span>';
            html += '<select class="prop-input">';
            html += '<option value="true"' + (a.value === true ? ' selected' : '') + '>true</option>';
            html += '<option value="false"' + (a.value === false ? ' selected' : '') + '>false</option>';
            html += '</select></div>';
        } else if (a.type === 'toggleVariable') {
            html += '<div class="prop-row"><span class="prop-label">变量名</span><input class="prop-input" type="text" value="' + esc(a.variableName || '') + '"></div>';
        } else if (a.type === 'logMessage') {
            html += '<div class="prop-row"><span class="prop-label">消息</span><input class="prop-input" type="text" value="' + esc(a.message || '') + '"></div>';
        } else if (a.type === 'switchPhase') {
            html += '<div class="prop-row"><span class="prop-label">目标阶段</span><input class="prop-input" type="text" value="' + esc(a.targetPhase || '') + '"></div>';
            html += '<div class="prop-row"><span class="prop-label">标签</span><input class="prop-input" type="text" value="' + esc(a.label || '') + '"></div>';
        } else if (a.type === 'switchBranch') {
            html += '<div class="prop-row"><span class="prop-label">条件变量</span><input class="prop-input" type="text" value="' + esc(a.condition || '') + '"></div>';
            html += '<div class="prop-row"><span class="prop-label">目标分支</span><input class="prop-input" type="text" value="' + esc(a.targetBranch || '') + '"></div>';
        }

        // 删除按钮
        html += '<button class="btn danger" style="margin-top:4px;">×</button>';
        html += '</div>'; // end action-item
    }

    // 添加动作按钮
    html += '<button class="btn" style="margin-top:4px;">+ 添加动作</button>';

    html += '</div>'; // end 动作

    // ==================== 删除事件 ====================
    html += '<button class="btn danger" style="width:100%;">删除事件</button>';

    panel.innerHTML = html;
}

function updateFooter() {
    var el = document.getElementById('footer');
    if (el) el.textContent = currentFile ? currentFile + (isDirty ? ' (未保存)' : '') : '就绪';
}

// ==================== 页面初始化 ====================

document.addEventListener('DOMContentLoaded', function() {
    if (!timelineData) {
        timelineData = newTimeline();
    }
    renderAll();
    updateFooter();
});
