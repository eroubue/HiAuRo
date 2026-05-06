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
    logMessage:       '#94a3b8',  // 灰色 - 日志（提高亮度确保在深色背景可见）
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

// ==================== 右键菜单状态 ====================

var ctxMenuClickTime = 0;     // 画布右键点击计算的时间位置
var ctxMenuTargetPath = '';   // 当前右键目标事件路径
var dragState = { active: false, srcPath: null, startY: 0, moved: false, preventClick: false };
var _dragHandlersBound = false; // 防止重复绑定拖拽事件

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
    if (markDirty._timer) clearTimeout(markDirty._timer);
    markDirty._timer = setTimeout(function() {
        isDirty = true;
        renderAll();
        updateFooter();
    }, 30);
}

// ==================== 渲染 (占位 — T5~T8 实现) ====================

function renderAll() { renderPhaseList(); renderPhaseTabs(); renderTimeScale(); renderPhaseTracks(); renderEvents(); renderProps(); bindDragHandlers(); }

// ==================== 阶段管理 ====================

/** 切换到指定阶段 */
function switchPhase(idx) {
    if (!timelineData || !timelineData.phases) return;
    if (idx < 0 || idx >= timelineData.phases.length) return;
    currentPhaseIdx = idx;
    selectedEventPath = null;
    renderAll();
}

/** 渲染左侧阶段列表 */
function renderPhaseList() {
    var el = document.getElementById('phaseList');
    if (!el) return;

    if (!timelineData || !timelineData.phases) {
        el.innerHTML = '<div class="hint">暂无阶段</div>';
        return;
    }

    var phases = timelineData.phases;
    var html = '';
    for (var i = 0; i < phases.length; i++) {
        var active = (i === currentPhaseIdx) ? ' active' : '';
        html += '<div class="phase-item' + active + '" data-idx="' + i + '">';
        html += '<span class="phase-name" data-idx="' + i + '">' + esc(phases[i].name) + '</span>';
        if (phases.length > 1) {
            html += '<button class="phase-del" data-idx="' + i + '" title="删除阶段">×</button>';
        }
        html += '</div>';
    }

    var canAdd = phases.length < MAX_PHASES;
    html += '<button class="phase-add" id="btnAddPhase"' + (canAdd ? '' : ' disabled') + '>' + esc(canAdd ? '+ 添加阶段' : '已达上限') + '</button>';

    el.innerHTML = html;

    // 阶段点击切换
    el.querySelectorAll('.phase-item').forEach(function(item) {
        item.addEventListener('click', function(e) {
            if (e.target.closest('.phase-del')) return;
            var idx = parseInt(this.dataset.idx);
            if (!isNaN(idx) && idx !== currentPhaseIdx) {
                switchPhase(idx);
            }
        });
    });

    // 双击重命名
    el.querySelectorAll('.phase-name').forEach(function(span) {
        span.addEventListener('dblclick', function(e) {
            e.stopPropagation();
            var idx = parseInt(this.dataset.idx);
            if (!isNaN(idx)) startRename(idx, this);
        });
    });

    // 删除按钮
    el.querySelectorAll('.phase-del').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.stopPropagation();
            var idx = parseInt(this.dataset.idx);
            if (!isNaN(idx)) deletePhase(idx);
        });
    });

    // 添加按钮
    var addBtn = document.getElementById('btnAddPhase');
    if (addBtn && !addBtn.disabled) {
        addBtn.addEventListener('click', addPhase);
    }
}

/** 渲染工具栏阶段选项卡 */
function renderPhaseTabs() {
    var el = document.getElementById('phaseTabs');
    if (!el) return;

    if (!timelineData || !timelineData.phases || timelineData.phases.length === 0) {
        el.innerHTML = '';
        return;
    }

    var html = '';
    for (var i = 0; i < timelineData.phases.length; i++) {
        var active = (i === currentPhaseIdx) ? ' active' : '';
        html += '<button class="tab' + active + '" data-idx="' + i + '" onclick="switchPhase(' + i + ')">' + esc(timelineData.phases[i].name) + '</button>';
    }
    el.innerHTML = html;

    // 多阶段时允许横向滚动
    el.style.overflowX = 'auto';
    el.style.flexWrap = 'nowrap';
}

/** 双击行内重命名 */
function startRename(idx, span) {
    var phase = getPhase(idx);
    if (!phase) return;

    var input = document.createElement('input');
    input.type = 'text';
    input.className = 'phase-rename-input';
    input.value = phase.name;

    span.style.display = 'none';
    span.parentNode.insertBefore(input, span.nextSibling);
    input.focus();
    input.select();

    function finish() {
        var val = input.value.trim();
        if (val && val !== phase.name) {
            phase.name = val;
            markDirty();
            return; // markDirty 已通过 renderAll 重绘了列表
        }
        // 取消或无变化 — 恢复显示
        input.remove();
        span.style.display = '';
    }

    input.addEventListener('blur', finish);
    input.addEventListener('keydown', function(e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            input.blur();
        } else if (e.key === 'Escape') {
            input.value = phase.name; // 重置
            input.blur();
        }
    });
}

/** 添加新阶段 */
function addPhase() {
    if (!timelineData) return;
    if (timelineData.phases.length >= MAX_PHASES) return;

    var newId = 'p' + (timelineData.phases.length + 1);
    timelineData.phases.push({
        id: newId,
        name: '新阶段',
        events: [],
        switch: null
    });
    currentPhaseIdx = timelineData.phases.length - 1;
    selectedEventPath = null;
    markDirty();
}

/** 删除阶段 */
function deletePhase(idx) {
    if (!timelineData || timelineData.phases.length <= 1) return;
    if (!confirm('确定删除阶段 "' + timelineData.phases[idx].name + '"？')) return;

    timelineData.phases.splice(idx, 1);

    // 调整 currentPhaseIdx
    if (idx < currentPhaseIdx) {
        currentPhaseIdx--;
    }
    if (currentPhaseIdx >= timelineData.phases.length) {
        currentPhaseIdx = timelineData.phases.length - 1;
    }

    selectedEventPath = null;
    markDirty();
}

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
        el.innerHTML = '<div class="hint">暂无阶段数据，点击"新建"开始创建</div>';
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
    var lastRightTop = -9999; // 右侧最后一个可见标签的 top
    var lastLeftTop = -9999;  // 左侧最后一个可见标签的 top

    for (var i = 0; i < events.length; i++) {
        var ev = events[i];
        var top = ev.time / TIME_STEP * LINE_HEIGHT;

        // ---- 事件节点 ----
        var node = document.createElement('div');
        node.className = 'event-node';
        node.style.top = top + 'px';
        node.dataset.path = 'p' + currentPhaseIdx + '_ev' + i;
        node.title = esc(ev.name) + ' (' + formatTime(ev.time) + ')';

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
                if (dragState.preventClick) return;
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

        // 防重叠：与同一侧标签垂直距离 < 40px 则换侧；换侧后仍重叠则隐藏
        var distRight = Math.abs(top - lastRightTop);
        var distLeft = Math.abs(top - lastLeftTop);
        var useLeft = false;
        var hideLabel = false;

        if (distRight < 40) {
            useLeft = true;
            if (distLeft < 40) {
                hideLabel = true;
            }
        }

        if (hideLabel) {
            label.style.display = 'none';
        } else if (useLeft) {
            label.className = 'event-label-alt';
            lastLeftTop = top;
        } else {
            label.className = 'event-label';
            lastRightTop = top;
        }

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
            node.title = esc(ev.name) + ' (' + formatTime(ev.time) + ')';
            node.style.background = brColor;
            node.style.boxShadow = '0 0 8px ' + brColor;

            if (selectedEventPath === path) {
                node.classList.add('sel');
            }

            // 点击选中
            (function(p, el) {
                el.addEventListener('click', function(e) {
                    if (dragState.preventClick) return;
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
    html += '<input class="prop-input" type="text" value="' + esc(ev.name || '') + '" onchange="updateEventProp(\'name\', this.value)">';
    html += '</div>';

    // ID (只读)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">ID</span>';
    html += '<input class="prop-input" type="text" value="' + esc(ev.id || '') + '" readonly>';
    html += '</div>';

    // 时间(s)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">时间(s)</span>';
    html += '<input class="prop-input" type="number" value="' + (ev.time || 0) + '" step="0.1" onchange="updateEventNumProp(\'time\', this.value)">';
    html += '</div>';

    // 持续(s)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">持续(s)</span>';
    html += '<input class="prop-input" type="number" value="' + (ev.duration || 0) + '" step="0.1" onchange="updateEventNumProp(\'duration\', this.value)">';
    html += '</div>';

    html += '</div>'; // end 基本信息

    // ==================== 同步校准 ====================
    html += '<div class="prop-section">';
    html += '<div class="prop-section-header">同步校准</div>';

    // 开始同步
    if (ev.startSync) {
        html += '<div style="margin-bottom:8px;">';
        html += '<span style="display:inline-block;width:60px;font-size:11px;color:var(--tx1);">开始同步</span>';
        html += '<select class="prop-input" style="width:auto;" onchange="updateSyncProp(\'startSync\',\'type\',this.value)">';
        html += '<option value="startsUsing"' + (ev.startSync.type === 'startsUsing' ? ' selected' : '') + '>读条</option>';
        html += '<option value="ability"' + (ev.startSync.type === 'ability' ? ' selected' : '') + '>技能</option>';
        html += '<option value="inCombat"' + (ev.startSync.type === 'inCombat' ? ' selected' : '') + '>进战</option>';
        html += '</select>';
        html += '</div>';
        html += '<div class="prop-row">';
        html += '<span class="prop-label">技能ID</span>';
        html += '<input class="prop-input" type="text" value="' + esc((ev.startSync.abilityIds || []).join(',')) + '" placeholder="逗号分隔" onchange="updateSyncProp(\'startSync\',\'abilityIds\',this.value)">';
        html += '</div>';
        html += '<button class="btn danger" style="margin-bottom:8px;" onclick="removeSync(\'startSync\')">移除</button>';
    } else {
        html += '<button class="btn" style="margin-bottom:4px;" onclick="addSync(\'startSync\')">+ 添加开始同步</button>';
    }

    // 结束同步
    if (ev.endSync) {
        html += '<div style="margin-bottom:8px;">';
        html += '<span style="display:inline-block;width:60px;font-size:11px;color:var(--tx1);">结束同步</span>';
        html += '<select class="prop-input" style="width:auto;" onchange="updateSyncProp(\'endSync\',\'type\',this.value)">';
        html += '<option value="startsUsing"' + (ev.endSync.type === 'startsUsing' ? ' selected' : '') + '>读条</option>';
        html += '<option value="ability"' + (ev.endSync.type === 'ability' ? ' selected' : '') + '>技能</option>';
        html += '<option value="inCombat"' + (ev.endSync.type === 'inCombat' ? ' selected' : '') + '>进战</option>';
        html += '</select>';
        html += '</div>';
        html += '<div class="prop-row">';
        html += '<span class="prop-label">技能ID</span>';
        html += '<input class="prop-input" type="text" value="' + esc((ev.endSync.abilityIds || []).join(',')) + '" placeholder="逗号分隔" onchange="updateSyncProp(\'endSync\',\'abilityIds\',this.value)">';
        html += '</div>';
        html += '<button class="btn danger" style="margin-bottom:8px;" onclick="removeSync(\'endSync\')">移除</button>';
    } else {
        html += '<button class="btn" onclick="addSync(\'endSync\')">+ 添加结束同步</button>';
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
        html += '<select class="prop-input" onchange="updateActionType(' + i + ', this.value)">';
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
            html += '<div class="prop-row"><span class="prop-label">减伤%</span><input class="prop-input" type="number" value="' + (a['需求减伤'] || 0) + '" onchange="updateActionProp(' + i + ', \'需求减伤\', parseFloat(this.value) || 0)"></div>';
            html += '<div class="prop-row"><span class="prop-label">治疗</span><input class="prop-input" type="number" value="' + (a['需求治疗'] || 0) + '" onchange="updateActionProp(' + i + ', \'需求治疗\', parseFloat(this.value) || 0)"></div>';
        } else if (a.type === 'skillSuggestion') {
            html += '<div class="prop-row"><span class="prop-label">技能ID</span><input class="prop-input" type="number" value="' + (a.skillId || 0) + '" onchange="updateActionProp(' + i + ', \'skillId\', parseInt(this.value) || 0)"></div>';
            html += '<div class="prop-row"><span class="prop-label">名称</span><input class="prop-input" type="text" value="' + esc(a.label || '') + '" onchange="updateActionProp(' + i + ', \'label\', this.value)"></div>';
            html += '<div class="prop-row"><span class="prop-label">优先级</span>';
            html += '<select class="prop-input" onchange="updateActionProp(' + i + ', \'priority\', this.value)">';
            html += '<option value="high"' + (a.priority === 'high' ? ' selected' : '') + '>high</option>';
            html += '<option value="normal"' + (a.priority === 'normal' ? ' selected' : '') + '>normal</option>';
            html += '<option value="optional"' + (a.priority === 'optional' ? ' selected' : '') + '>optional</option>';
            html += '</select></div>';
        } else if (a.type === 'setVariable') {
            html += '<div class="prop-row"><span class="prop-label">变量名</span><input class="prop-input" type="text" value="' + esc(a.variableName || '') + '" onchange="updateActionProp(' + i + ', \'variableName\', this.value)"></div>';
            html += '<div class="prop-row"><span class="prop-label">值</span>';
            html += '<select class="prop-input" onchange="updateActionProp(' + i + ', \'value\', this.value === \'true\')">';
            html += '<option value="true"' + (a.value === true ? ' selected' : '') + '>true</option>';
            html += '<option value="false"' + (a.value === false ? ' selected' : '') + '>false</option>';
            html += '</select></div>';
        } else if (a.type === 'toggleVariable') {
            html += '<div class="prop-row"><span class="prop-label">变量名</span><input class="prop-input" type="text" value="' + esc(a.variableName || '') + '" onchange="updateActionProp(' + i + ', \'variableName\', this.value)"></div>';
        } else if (a.type === 'logMessage') {
            html += '<div class="prop-row"><span class="prop-label">消息</span><input class="prop-input" type="text" value="' + esc(a.message || '') + '" onchange="updateActionProp(' + i + ', \'message\', this.value)"></div>';
        } else if (a.type === 'switchPhase') {
            html += '<div class="prop-row"><span class="prop-label">目标阶段</span><input class="prop-input" type="text" value="' + esc(a.targetPhase || '') + '" onchange="updateActionProp(' + i + ', \'targetPhase\', this.value)"></div>';
            html += '<div class="prop-row"><span class="prop-label">标签</span><input class="prop-input" type="text" value="' + esc(a.label || '') + '" onchange="updateActionProp(' + i + ', \'label\', this.value)"></div>';
            html += '<button class="btn" style="margin-top:2px;font-size:10px;padding:2px 8px" onclick="createPhaseForAction(' + i + ')">＋ 新建阶段并关联</button>';
        } else if (a.type === 'switchBranch') {
            html += '<div class="prop-row"><span class="prop-label">条件变量</span><input class="prop-input" type="text" value="' + esc(a.condition || '') + '" onchange="updateActionProp(' + i + ', \'condition\', this.value)"></div>';
            html += '<div class="prop-row"><span class="prop-label">目标分支</span><input class="prop-input" type="text" value="' + esc(a.targetBranch || '') + '" onchange="updateActionProp(' + i + ', \'targetBranch\', this.value)"></div>';
            html += '<button class="btn" style="margin-top:2px;font-size:10px;padding:2px 8px" onclick="createBranchForAction(' + i + ')">＋ 新建分支并关联</button>';
        }

        // 删除按钮
        html += '<button class="btn danger" style="margin-top:4px;" onclick="deleteAction(' + i + ')">×</button>';
        html += '</div>'; // end action-item
    }

    // 添加动作按钮
    html += '<button class="btn" style="margin-top:4px;" onclick="addAction()">+ 添加动作</button>';

    html += '</div>'; // end 动作

    // ==================== 删除事件 ====================
    html += '<button class="btn danger" style="width:100%;" onclick="deleteEvent()">删除事件</button>';

    panel.innerHTML = html;
}

// ==================== 属性面板事件处理器 ====================

function updateEventProp(field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    ev[field] = value;
    markDirty();
}

function updateEventNumProp(field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    ev[field] = parseFloat(value) || 0;
    markDirty();
}

function addSync(side) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    ev[side] = { type: 'startsUsing', abilityIds: [] };
    markDirty();
}

function removeSync(side) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    delete ev[side];
    markDirty();
}

function updateSyncProp(side, field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev[side]) return;
    if (field === 'abilityIds') {
        ev[side][field] = value ? value.split(',').map(function(s) { return parseInt(s.trim()) || 0; }).filter(function(id) { return id > 0; }) : [];
    } else {
        ev[side][field] = value;
    }
    markDirty();
}

function updateActionType(idx, newType) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions) return;
    ev.actions[idx] = JSON.parse(JSON.stringify(ACTION_TEMPLATES[newType]));
    markDirty();
}

function updateActionProp(idx, field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions || !ev.actions[idx]) return;
    ev.actions[idx][field] = value;
    markDirty();
}

function deleteAction(idx) {
    if (!confirm('确定删除此动作?')) return;
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions) return;
    ev.actions.splice(idx, 1);
    markDirty();
}

function addAction() {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    if (!ev.actions) ev.actions = [];
    ev.actions.push(JSON.parse(JSON.stringify(ACTION_TEMPLATES.skillSuggestion)));
    markDirty();
}

/** 为 switchPhase 动作新建阶段并关联 */
function createPhaseForAction(actionIdx) {
    if (!timelineData || !timelineData.phases) return;
    if (timelineData.phases.length >= MAX_PHASES) { showError('已达阶段上限 (' + MAX_PHASES + ')'); return; }
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions || !ev.actions[actionIdx]) return;
    var newPhase = { id: 'p' + (timelineData.phases.length + 1), name: '新阶段', events: [], switch: null };
    timelineData.phases.push(newPhase);
    ev.actions[actionIdx].targetPhase = newPhase.id;
    markDirty();
}

/** 为 switchBranch 动作新建分支并关联 */
function createBranchForAction(actionIdx) {
    var phase = getPhase(currentPhaseIdx);
    if (!phase) return;
    if (!phase.switch) {
        phase.switch = { sync: { type: 'startsUsing', abilityIds: [], entering: true }, branches: [] };
    }
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions || !ev.actions[actionIdx]) return;
    var newBranch = { name: '新分支', events: [] };
    phase.switch.branches.push(newBranch);
    ev.actions[actionIdx].targetBranch = newBranch.name;
    markDirty();
}

function deleteEvent() {
    if (!confirm('确定删除此事件?')) return;
    var info = getParentInfo(selectedEventPath);
    if (!info) return;
    info.container.splice(info.idx, 1);
    selectedEventPath = null;
    markDirty();
}

function updateFooter() {
    var el = document.getElementById('footer');
    if (el) el.textContent = currentFile ? currentFile + (isDirty ? ' (未保存)' : '') : '就绪';
}

function showError(msg) {
    var el = document.getElementById('footer');
    if (!el) return;
    el.textContent = msg;
    el.style.color = 'var(--red)';
    setTimeout(function() {
        el.style.color = '';
        updateFooter();
    }, 3000);
}

function showLoading(msg) {
    var el = document.getElementById('footer');
    if (!el) return;
    el.textContent = msg;
    el.style.color = '';
}

// ==================== 文件操作 ====================

function newFile() {
    if (timelineData && isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    timelineData = newTimeline();
    currentFile = '';
    fileHandle = null;
    isDirty = false;
    currentPhaseIdx = 0;
    selectedEventPath = null;
    renderAll();
    updateFooter();
}

function loadFile() {
    if (timelineData && isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    document.getElementById('fileInput').click();
}

function readFileObj(file) {
    showLoading('加载中...');
    var reader = new FileReader();
    reader.onload = function() {
        try {
            var data = JSON.parse(reader.result);
            timelineData = data;
            currentFile = file.name;
            fileHandle = null;
            isDirty = false;
            currentPhaseIdx = 0;
            selectedEventPath = null;
            renderAll();
            updateFooter();
        } catch (ex) {
            showError('JSON解析失败: ' + esc(ex.message));
        }
    };
    reader.onerror = function() {
        showError('文件读取失败');
    };
    reader.readAsText(file);
}

async function saveFile() {
    if (!timelineData) return;
    var json = JSON.stringify(timelineData, null, 2);
    if (fileHandle && typeof fileHandle.createWritable === 'function') {
        try {
            var writable = await fileHandle.createWritable();
            await writable.write(json);
            await writable.close();
            isDirty = false;
            updateFooter();
            return;
        } catch (e) {
            // 降级到 Save As
        }
    }
    saveFileAs();
}

async function saveFileAs() {
    if (!timelineData) return;
    var json = JSON.stringify(timelineData, null, 2);
    var name = currentFile || 'timeline.json';
    if (window.showSaveFilePicker) {
        try {
            var handle = await window.showSaveFilePicker({
                suggestedName: name,
                types: [{ description: 'JSON', accept: { 'application/json': ['.json'] } }]
            });
            var writable = await handle.createWritable();
            await writable.write(json);
            await writable.close();
            fileHandle = handle;
            currentFile = handle.name;
            isDirty = false;
            updateFooter();
            return;
        } catch (e) {
            if (e.name === 'AbortError') return;
        }
    }
    downloadJson(json, name);
}

function exportFile() {
    if (!timelineData) return;
    var json = JSON.stringify(timelineData, null, 2);
    var name = currentFile || 'timeline.json';
    downloadJson(json, name);
}

function downloadJson(json, name) {
    var blob = new Blob([json], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = name;
    a.click();
    URL.revokeObjectURL(url);
}

// ==================== 拖拽 (垂直时间调整) ====================

function bindDragHandlers() {
    if (_dragHandlersBound) return;
    _dragHandlersBound = true;

    var tracksEl = document.getElementById('phaseTracks');
    if (!tracksEl) return;

    // -- 创建 drop indicator (复用) --
    var dropIndicator = document.createElement('div');
    dropIndicator.className = 'drop-indicator';
    dropIndicator.style.display = 'none';
    tracksEl.appendChild(dropIndicator);

    // -- mousedown: 事件代理在 #phaseTracks --
    tracksEl.addEventListener('mousedown', function(e) {
        if (e.button !== 0) return; // 只响应左键
        var node = e.target.closest('.event-node, .sub-branch-event');
        if (!node) return;
        dragState.active = true;
        dragState.srcPath = node.dataset.path;
        dragState.startY = e.clientY;
        dragState.moved = false;
        e.preventDefault(); // 防止文本选中
    });

    // -- mousemove: 全局 --
    document.addEventListener('mousemove', function(e) {
        if (!dragState.active) return;
        if (dragState.moved) return; // 已经移动过, 跳过重复计算
        if (Math.abs(e.clientY - dragState.startY) < 4) return; // 阈值

        dragState.moved = true;

        // 给源节点添加 .dragging 类
        var srcNode = document.querySelector('[data-path="' + dragState.srcPath + '"]');
        if (srcNode) {
            srcNode.classList.add('dragging');
        }

        // 显示 drop indicator
        dropIndicator.style.display = 'block';
    });

    // -- mouseup: 全局 --
    document.addEventListener('mouseup', function(e) {
        if (!dragState.active) return;

        var wasMoved = dragState.moved;
        var srcPath = dragState.srcPath;

        // 重置拖拽状态
        var srcNode = document.querySelector('[data-path="' + srcPath + '"]');
        if (srcNode) {
            srcNode.classList.remove('dragging');
        }
        dropIndicator.style.display = 'none';
        dragState.active = false;
        dragState.srcPath = null;
        dragState.moved = false;

        if (!wasMoved) return;

        // 计算新时间
        var canvasEl = document.getElementById('timelineCanvas');
        if (!canvasEl) return;
        var rect = canvasEl.getBoundingClientRect();
        var scrollEl2 = canvasEl.querySelector('.timeline-scroll');
        var scrollTop = scrollEl2 ? scrollEl2.scrollTop : 0;
        var rawTime = (e.clientY - rect.top + scrollTop) / LINE_HEIGHT * TIME_STEP;
        var newTime = Math.max(0, Math.min(MAX_TIME, Math.round(rawTime / TIME_STEP) * TIME_STEP));

        // 解析事件并更新时间
        var ev = getEventByPath(srcPath);
        if (!ev) return;
        ev.time = newTime;
        markDirty();

        // 防止后续 click 事件误触发
        dragState.preventClick = true;
        setTimeout(function() { dragState.preventClick = false; }, 50);
    });
}

// ==================== 右键菜单 ====================

/**
 * 在 (x, y) 处显示右键菜单
 * @param {number} x 屏幕 x 坐标 (clientX)
 * @param {number} y 屏幕 y 坐标 (clientY)
 * @param {Array} items 菜单项数组: { label, action, danger, separator }
 */
function showContextMenu(x, y, items) {
    var menu = document.getElementById('ctxMenu');
    if (!menu) return;

    // 锁定横向滚动，防止菜单与画布内容错位
    var scrollEl = document.querySelector('.timeline-scroll');
    if (scrollEl) {
        if (scrollEl.dataset.savedScrollLeft === undefined) {
            scrollEl.dataset.savedScrollLeft = scrollEl.scrollLeft;
        }
        scrollEl.style.overflowX = 'hidden';
    }

    var html = '';
    for (var i = 0; i < items.length; i++) {
        var item = items[i];
        if (item.separator) {
            html += '<div class="ctx-sep"></div>';
        } else {
            var cls = 'ctx-item' + (item.danger ? ' ctx-danger' : '');
            html += '<div class="' + cls + '" data-action="' + esc(item.action) + '">' + esc(item.label) + '</div>';
        }
    }
    menu.innerHTML = html;

    menu.style.left = x + 'px';
    menu.style.top = y + 'px';
    menu.classList.remove('hide');

    // 菜单项点击
    menu.onclick = function(e) {
        var target = e.target.closest('.ctx-item');
        if (!target) return;
        e.stopPropagation();
        handleContextAction(target.dataset.action);
    };
}

/** 隐藏右键菜单 */
function hideContextMenu() {
    var menu = document.getElementById('ctxMenu');
    if (!menu) return;
    menu.innerHTML = '';
    menu.classList.add('hide');

    // 恢复横向滚动
    var scrollEl = document.querySelector('.timeline-scroll');
    if (scrollEl && scrollEl.dataset.savedScrollLeft !== undefined) {
        scrollEl.style.overflowX = '';
        scrollEl.scrollLeft = parseInt(scrollEl.dataset.savedScrollLeft);
        delete scrollEl.dataset.savedScrollLeft;
    }
}

/** 处理菜单动作分发 */
function handleContextAction(action) {
    switch (action) {
        case 'addEvent': {
            // 在当前阶段添加新事件
            var phase = getPhase(currentPhaseIdx);
            if (!phase) break;
            var newEv = {
                id: 'ev' + (phase.events.length + 1),
                name: '新事件',
                time: ctxMenuClickTime,
                duration: 0,
                actions: []
            };
            phase.events.push(newEv);
            selectedEventPath = 'p' + currentPhaseIdx + '_ev' + (phase.events.length - 1);
            markDirty();
            break;
        }
        case 'addAction': {
            // 给当前右键目标事件添加一个默认动作
            var ev = getEventByPath(ctxMenuTargetPath);
            if (!ev) break;
            if (!ev.actions) ev.actions = [];
            ev.actions.push({
                type: 'demand',
                '需求减伤': 0,
                '需求治疗': 0
            });
            markDirty();
            break;
        }
        case 'deleteEvent': {
            // 删除事件（需要确认）
            if (!confirm('确认删除此事件？')) break;
            var info = getParentInfo(ctxMenuTargetPath);
            if (!info) break;
            info.container.splice(info.idx, 1);
            selectedEventPath = null;
            markDirty();
            break;
        }
    }
    hideContextMenu();
}

// ==================== 页面初始化 ====================

document.addEventListener('DOMContentLoaded', function() {
    if (!timelineData) {
        timelineData = newTimeline();
    }
    renderAll();
    updateFooter();

    // ---- 文件操作绑定 ----

    document.getElementById('btnNew').addEventListener('click', newFile);
    document.getElementById('btnLoad').addEventListener('click', loadFile);
    document.getElementById('btnSave').addEventListener('click', saveFile);
    document.getElementById('btnExport').addEventListener('click', exportFile);
    document.getElementById('fileInput').addEventListener('change', function() {
        if (this.files && this.files[0]) {
            readFileObj(this.files[0]);
            this.value = '';
        }
    });

    // ---- 右键菜单事件绑定 ----

    // 画布空白处右键 → 添加事件
    var canvas = document.getElementById('timelineCanvas');
    if (canvas) {
        canvas.addEventListener('contextmenu', function(e) {
            e.preventDefault();
            e.stopPropagation();

            var rect = canvas.getBoundingClientRect();
            var scrollEl3 = canvas.querySelector('.timeline-scroll');
            var scrollTop3 = scrollEl3 ? scrollEl3.scrollTop : 0;
            var y = e.clientY - rect.top + scrollTop3;
            ctxMenuClickTime = Math.round(y / LINE_HEIGHT) * TIME_STEP;
            if (ctxMenuClickTime < 0) ctxMenuClickTime = 0;
            if (ctxMenuClickTime > MAX_TIME) ctxMenuClickTime = MAX_TIME;

            showContextMenu(e.clientX, e.clientY, [
                { label: '添加事件', action: 'addEvent' }
            ]);
        });
    }

    // 事件节点/子分支事件右键 → 添加动作 / 删除事件
    var tracks = document.getElementById('phaseTracks');
    if (tracks) {
        tracks.addEventListener('contextmenu', function(e) {
            var node = e.target.closest('.event-node, .sub-branch-event');
            if (!node) return;

            e.preventDefault();
            e.stopPropagation();

            ctxMenuTargetPath = node.dataset.path;

            // 自动选中该事件
            selectedEventPath = ctxMenuTargetPath;
            var allNodes = tracks.querySelectorAll('.event-node, .sub-branch-event');
            for (var i = 0; i < allNodes.length; i++) {
                allNodes[i].classList.remove('sel');
            }
            node.classList.add('sel');
            renderProps();

            showContextMenu(e.clientX, e.clientY, [
                { label: '添加动作', action: 'addAction' },
                { label: '删除事件', action: 'deleteEvent', danger: true }
            ]);
        });
    }

    // 点击菜单外部任意位置 → 关闭菜单
    document.addEventListener('mousedown', function(e) {
        var menu = document.getElementById('ctxMenu');
        if (!menu || menu.classList.contains('hide')) return;
        if (!menu.contains(e.target)) {
            hideContextMenu();
        }
    });

    // ---- 鼠标时间标线 ----
    var mouseMarker = document.getElementById('mouseMarker');
    var mouseMarkerLabel = mouseMarker ? mouseMarker.querySelector('.mouse-marker-label') : null;
    var scrollEl = canvas ? canvas.querySelector('.timeline-scroll') : null;
    if (canvas) {
        canvas.addEventListener('mousemove', function(e) {
            if (dragState.active && dragState.moved) return; // 拖拽时不显示
            if (!mouseMarker) return;
            mouseMarker.classList.remove('hide');
            var rect = canvas.getBoundingClientRect();
            var scrollTop = scrollEl ? scrollEl.scrollTop : 0;
            var y = e.clientY - rect.top + scrollTop;
            var time = y / LINE_HEIGHT * TIME_STEP;
            time = Math.round(time * 10) / 10; // 精度 0.1s
            mouseMarker.style.top = (e.clientY - rect.top) + 'px';
            if (mouseMarkerLabel) {
                mouseMarkerLabel.textContent = formatTime(time);
            }
        });
        canvas.addEventListener('mouseleave', function() {
            if (mouseMarker) mouseMarker.classList.add('hide');
        });
    }

    // ==================== 键盘快捷键 ====================

    document.addEventListener('keydown', function(e) {
        // Ctrl+S / Cmd+S → 触发生成器的保存按钮
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            var saveBtn = document.getElementById('btnSave');
            if (saveBtn) saveBtn.click();
            return;
        }

        // Delete → 删除选中事件（跳过编辑框）
        if (e.key === 'Delete' && selectedEventPath) {
            var tag = e.target.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA') return;
            e.preventDefault();
            if (confirm('确认删除此事件？')) {
                var info = getParentInfo(selectedEventPath);
                if (!info) return;
                info.container.splice(info.idx, 1);
                selectedEventPath = null;
                markDirty();
            }
            return;
        }

        // Escape → 关闭右键菜单，清除事件选中
        if (e.key === 'Escape') {
            hideContextMenu();
            if (selectedEventPath) {
                selectedEventPath = null;
                var tracks = document.getElementById('phaseTracks');
                if (tracks) {
                    tracks.querySelectorAll('.event-node, .sub-branch-event').forEach(function(n) {
                        n.classList.remove('sel');
                    });
                }
                renderProps();
            }
            return;
        }
    });
});
