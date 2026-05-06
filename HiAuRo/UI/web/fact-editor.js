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

function renderTimeScale() {}

function renderPhaseTracks() {}

function renderEvents() {}

function renderProps() {}

function updateFooter() {
    var el = document.getElementById('footer');
    if (el) el.textContent = currentFile ? currentFile + (isDirty ? ' (未保存)' : '') : '就绪';
}
