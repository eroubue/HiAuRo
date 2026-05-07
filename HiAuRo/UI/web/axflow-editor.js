// ============================================================
// HiAuRo AxFlow 编辑器 — 基于 Drawflow
// ============================================================

// ====== Part 1: Constants ======

var NODE_DEFS = [
    { type: 'treeRoot',      label: 'Root',  color: '#30b0ff', cssClass: 'c-root',     composite: true },
    { type: 'treeSequence',  label: '序列',  color: '#30d158', cssClass: 'c-sequence', composite: true },
    { type: 'treeParallel',  label: '并行',  color: '#ff9f0a', cssClass: 'c-parallel', composite: true },
    { type: 'treeSelect',    label: '选择',  color: '#bf5af2', cssClass: 'c-select',   composite: true },
    { type: 'treeLoop',      label: '循环',  color: '#ff375f', cssClass: 'c-loop',     composite: true },
    { type: 'treeCondNode',  label: '条件',  color: '#15aabf', cssClass: 'c-cond',     composite: false },
    { type: 'treeActionNode',label: '动作',  color: '#f06595', cssClass: 'c-action',   composite: false },
    { type: 'treeDelayNode', label: '延迟',  color: '#fcc419', cssClass: 'c-delay',    composite: false },
    { type: 'treeScriptNode',label: '脚本',  color: '#4dabf7', cssClass: 'c-script',   composite: false }
];

var TYPE_FULL = {
    treeRoot:       'HiAuRo.Execution.TreeRoot, HiAuRo',
    treeSequence:   'HiAuRo.Execution.TreeSequence, HiAuRo',
    treeParallel:   'HiAuRo.Execution.TreeParallel, HiAuRo',
    treeSelect:     'HiAuRo.Execution.TreeSelect, HiAuRo',
    treeLoop:       'HiAuRo.Execution.TreeLoop, HiAuRo',
    treeCondNode:   'HiAuRo.Execution.TreeCondNode, HiAuRo',
    treeActionNode: 'HiAuRo.Execution.TreeActionNode, HiAuRo',
    treeDelayNode:  'HiAuRo.Execution.TreeDelayNode, HiAuRo',
    treeScriptNode: 'HiAuRo.Execution.TreeScriptNode, HiAuRo'
};

// ====== Part 2: State ======

var currentAxis = 'execution';
var currentFile = '';
var fileHandle = null;
var isDirty = false;
var AXIS_DATA = { execution: null, assist: null };
var editor = null;
var dfIdToData = {};       // drawflowId → tree node data
var treeIdToDfId = {};     // tree node Id → drawflowId
var selectedDfId = null;
var dirHandle = null;
var fileEntries = [];
var autoIdCounter = 100;



// ====== Part 3: Init ======

document.addEventListener('DOMContentLoaded', function() {
    initToolbar();
    initPalette();
    initDrawflow();
    initCanvasBtns();
    initKeyboard();
    updateInfo();

});

function initToolbar() {
    document.querySelectorAll('.tab-btn').forEach(function(btn) {
        btn.addEventListener('click', function() {
            if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
            document.querySelectorAll('.tab-btn').forEach(function(b) { b.classList.remove('active'); });
            btn.classList.add('active');
            currentAxis = btn.dataset.axis;
            switchAxis();
        });
    });
    document.getElementById('btnNew').addEventListener('click', newFile);
    document.getElementById('btnLoad').addEventListener('click', loadFile);
    document.getElementById('btnSave').addEventListener('click', saveFile);
    document.getElementById('btnSaveAs').addEventListener('click', saveFileAs);
    document.getElementById('btnExport').addEventListener('click', exportFile);
    document.getElementById('btnImport').addEventListener('click', function() {
        document.getElementById('edFileInput').click();
    });
    document.getElementById('edFileInput').addEventListener('change', function() {
        var f = this.files[0]; if (f) { readFileObj(f); this.value = ''; }
    });
    document.getElementById('axisName').addEventListener('change', function() {
        if (!AXIS_DATA[currentAxis]) return;
        AXIS_DATA[currentAxis].Name = this.value;
        markDirty();
    });
}

function switchAxis() {
    var data = AXIS_DATA[currentAxis];
    if (data && data.TreeRoot) {
        importTreeToDrawflow(data);
        document.getElementById('axisName').value = data.Name || '';
    } else {
        editor.clear();
        dfIdToData = {};
        treeIdToDfId = {};
        selectedDfId = null;
        document.getElementById('axisName').value = '';
    }
    renderProps();
    updateInfo();
    updateFooter();
}

// ====== Part 4: Drawflow Setup ======

function initDrawflow() {
    var container = document.getElementById('drawflow');
    editor = new Drawflow(container);
    editor.reroute = true;
    editor.reroute_fix_curvature = true;
    editor.start();

    editor.on('nodeSelected', function(id) {
        selectedDfId = id;
        renderProps();
    });

    editor.on('nodeUnselected', function() {
        selectedDfId = null;
        renderProps();
    });

    editor.on('nodeRemoved', function(id) {
        delete dfIdToData[id];
        if (selectedDfId === id) { selectedDfId = null; renderProps(); }
        markDirty();
    });

    editor.on('nodeMoved', function(id) {
        markDirty();
    });

    editor.on('connectionCreated', function() {
        markDirty();
    });

    editor.on('connectionRemoved', function() {
        markDirty();
    });

    editor.on('nodeCreated', function(id) {
        markDirty();
    });

    editor.on('contextmenu', function(ev) {
        ev.preventDefault();
        showContextMenu(ev);
    });
}

function initCanvasBtns() {
    document.getElementById('btnCanvasExport').addEventListener('click', function() {
        var json = JSON.stringify(exportTreeFromDrawflow(), null, 2);
        if (confirm('导出 JSON 内容：\n\n' + json.substring(0, 500) + (json.length > 500 ? '...' : '') + '\n\n复制到剪贴板？')) {
            navigator.clipboard.writeText(json).then(function() {
                setStatus('已复制到剪贴板', 'ok');
            });
        }
    });
    document.getElementById('btnCanvasClear').addEventListener('click', function() {
        if (confirm('确定清空画布？')) {
            editor.clear();
            dfIdToData = {};
            treeIdToDfId = {};
            selectedDfId = null;
            renderProps();
            markDirty();
        }
    });
    document.getElementById('btnZoomIn').addEventListener('click', function() { editor.zoom_in(); });
    document.getElementById('btnZoomOut').addEventListener('click', function() { editor.zoom_out(); });
    document.getElementById('btnZoomReset').addEventListener('click', function() { editor.zoom_reset(); });
}

// ====== Part 5: Palette Drag-Drop ======

function initPalette() {
    var list = document.getElementById('paletteList');
    var html = '';
    NODE_DEFS.forEach(function(def, i) {
        if (def.type === 'treeRoot') return; // Root 不从面板拖入，由新建自动创建
        html += '<div class="palette-item" draggable="true" data-node-type="' + def.type + '">' +
            '<span class="palette-dot" style="background:' + def.color + '"></span>' +
            '<span>' + def.label + '</span>' +
            '</div>';
    });
    list.innerHTML = html;

    // 绑定拖拽事件
    list.querySelectorAll('.palette-item').forEach(function(item) {
        item.addEventListener('dragstart', drag);
    });

    // 移动端触控支持
    var mobileItem = '';
    var mobileLast = null;
    list.querySelectorAll('.palette-item').forEach(function(item) {
        item.addEventListener('touchstart', function(ev) {
            mobileItem = ev.target.closest('.palette-item').getAttribute('data-node-type');
        });
    });

    document.addEventListener('touchend', function(ev) {
        if (!mobileItem) return;
        var target = document.elementFromPoint(mobileLast.touches[0].clientX, mobileLast.touches[0].clientY);
        if (target && target.closest('#drawflow')) {
            addNodeFromPalette(mobileItem, mobileLast.touches[0].clientX, mobileLast.touches[0].clientY);
        }
        mobileItem = '';
    });

    document.addEventListener('touchmove', function(ev) {
        mobileLast = ev;
    });
}

function drag(ev) {
    ev.dataTransfer.setData('node-type', ev.target.closest('.palette-item').getAttribute('data-node-type'));
}

function allowDrop(ev) {
    ev.preventDefault();
}

function drop(ev) {
    ev.preventDefault();
    var nodeType = ev.dataTransfer.getData('node-type');
    if (!nodeType) return;
    addNodeFromPalette(nodeType, ev.clientX, ev.clientY);
}

function addNodeFromPalette(nodeType, clientX, clientY) {
    // 计算画布坐标
    var canvas = editor.precanvas;
    var rect = canvas.getBoundingClientRect();
    var posX = (clientX - rect.left) / editor.zoom;
    var posY = (clientY - rect.top) / editor.zoom;

    var def = getNodeDef(nodeType);
    var data = newNodeDefaults(nodeType);
    var childDfId = createDrawflowNode(def, data, posX, posY);
    
    // 如果有选中的复合节点，自动连接并插入兄弟链末尾
    if (selectedDfId && dfIdToData[selectedDfId]) {
        var selDef = getNodeDefByData(dfIdToData[selectedDfId]);
        if (selDef && selDef.composite && selectedDfId !== treeIdToDfId[data.Id]) {
            connectAsLastChild(selectedDfId, childDfId);
        }
    }
}

// ====== Part 6: Node Creation & Update ======

function createDrawflowNode(def, nodeData, posX, posY) {
    // Root: 0入1出; 其他节点: 2入2出 (左入=父, 上入=前兄弟; 右出=子, 下出=后兄弟)
    var inputs = def.type === 'treeRoot' ? 0 : 2;
    var outputs = def.type === 'treeRoot' ? 1 : 2;
    posX = Math.max(20, posX || 50);
    posY = Math.max(20, posY || 100);

    var html = buildNodeHtml(def, nodeData);
    var dfId = editor.addNode(def.type, inputs, outputs, posX, posY, def.cssClass, {}, html);
    
    dfIdToData[dfId] = nodeData;
    treeIdToDfId[nodeData.Id] = dfId;
    
    // 绑定双击编辑名称
    setTimeout(function() {
        var nodeEl = document.getElementById('node-' + dfId);
        if (nodeEl) {
            var nameEl = nodeEl.querySelector('.ax-node-name');
            if (nameEl) {
                nameEl.addEventListener('dblclick', function(e) {
                    e.stopPropagation();
                    editNodeNameInline(dfId, nameEl);
                });
            }
        }
    }, 50);

    return dfId;
}

function buildNodeHtml(def, nodeData) {
    var name = esc(nodeData.DisplayName || def.label);
    var extra = '';
    if (def.composite && nodeData.Childs) {
        var count = (nodeData.Childs || []).length;
        if (count > 0) extra = count + '子';
    }
    if (def.type === 'treeLoop') extra = '×' + (nodeData.Times || 1);
    if (def.type === 'treeDelayNode') extra = (nodeData.Delay || 0) + 's';

    return '<div>' +
        '<div class="ax-node-hdr ' + def.cssClass + '">' +
            '<span class="ax-node-type">' + def.label + '</span>' +
            '<span class="ax-node-extra">' + extra + '</span>' +
        '</div>' +
        '<div class="ax-node-body">' +
            '<div class="ax-node-name">' + name + '</div>' +
        '</div>' +
    '</div>';
}

function updateNodeView(dfId) {
    var data = dfIdToData[dfId];
    if (!data) return;
    var def = getNodeDefByData(data);

    var nodeEl = document.getElementById('node-' + dfId);
    if (!nodeEl) return;

    var nameEl = nodeEl.querySelector('.ax-node-name');
    if (nameEl) nameEl.textContent = data.DisplayName || def.label;

    var extraEl = nodeEl.querySelector('.ax-node-extra');
    if (extraEl) {
        var extra = '';
        if (def.type === 'treeLoop') extra = '×' + (data.Times || 1);
        else if (def.type === 'treeDelayNode') extra = (data.Delay || 0) + 's';
        else if (def.composite) {
            var count = (data.Childs || []).length;
            if (count > 0) extra = count + '子';
        }
        extraEl.textContent = extra;
    }
}

function editNodeNameInline(dfId, nameEl) {
    var original = nameEl.textContent;
    nameEl.contentEditable = 'true';
    nameEl.focus();
    // 选中所有文本
    var range = document.createRange();
    range.selectNodeContents(nameEl);
    var sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(range);

    nameEl.addEventListener('blur', function handler() {
        nameEl.removeEventListener('blur', handler);
        nameEl.contentEditable = 'false';
        var newName = nameEl.textContent.trim();
        if (newName && newName !== original && dfIdToData[dfId]) {
            dfIdToData[dfId].DisplayName = newName;
            markDirty();
        } else {
            nameEl.textContent = original;
        }
    });

    nameEl.addEventListener('keydown', function handler(e) {
        if (e.key === 'Enter') { e.preventDefault(); nameEl.blur(); }
    });
}

function recreateNode(dfId) {
    // 保存连线信息后重建节点
    var data = dfIdToData[dfId];
    var def = getNodeDefByData(data);
    if (!def) return;

    // 保存连接
    var nodeInfo = editor.getNodeFromId(dfId);
    if (!nodeInfo) return;
    var posX = nodeInfo.pos_x;
    var posY = nodeInfo.pos_y;
    var inConns = (nodeInfo.inputs && nodeInfo.inputs.input_1 && nodeInfo.inputs.input_1.connections) || [];
    var outConns = (nodeInfo.outputs && nodeInfo.outputs.output_1 && nodeInfo.outputs.output_1.connections) || [];

    // 删除旧节点
    editor.removeNodeId('node-' + dfId);
    delete dfIdToData[dfId];

    // 重建
    var newDfId = createDrawflowNode(def, data, posX, posY);
    dfIdToData[newDfId] = data;

    // 恢复连接
    inConns.forEach(function(c) {
        try { editor.addConnection(c.node, newDfId, c.output || 'output_1', c.input || 'input_1'); } catch(e) {}
    });
    outConns.forEach(function(c) {
        try { editor.addConnection(newDfId, c.node, 'output_1', c.input || 'input_1'); } catch(e) {}
    });

    if (selectedDfId === dfId) selectedDfId = newDfId;
    return newDfId;
}

// ====== Part 7: Data Conversion ======

function importTreeToDrawflow(data) {
    editor.clear();
    dfIdToData = {};
    treeIdToDfId = {};
    selectedDfId = null;

    AXIS_DATA[currentAxis] = {
        Name: data.Name || '新执行轴',
        TerritoryTypeId: data.TerritoryTypeId || 0,
        Note: data.Note || '',
        ExposedVars: data.ExposedVars || []
    };
    document.getElementById('axisName').value = AXIS_DATA[currentAxis].Name;

    if (!data.TreeRoot) return;

    var positions = (data._drawflow && data._drawflow.positions) || {};
    // 如果没有位置数据，自动布局
    var autoPos = !positions || Object.keys(positions).length === 0;
    var layout = {};
    if (autoPos) layout = autoLayout(data.TreeRoot);

    function walk(nodeData, parentDfId) {
        var def = getNodeDefByData(nodeData);
        var pos;
        if (!autoPos && positions[nodeData.Id]) {
            pos = positions[nodeData.Id];
        } else if (autoPos && layout[nodeData.Id]) {
            pos = layout[nodeData.Id];
        } else {
            pos = [100 + Math.random() * 400, 100 + Math.random() * 300];
        }

        var dfId = createDrawflowNode(def, nodeData, pos[0], pos[1]);

        if (parentDfId !== null) {
            try { editor.addConnection(parentDfId, dfId, 'output_1', 'input_1'); } catch(e) {}
        }

        var children = nodeData.Childs || [];
        if (children.length === 0) return dfId;

        // 序列/循环：父连第一个子（output_1），兄弟链走 output_2→input_2
        // 并行/选择：父连所有子（output_1），兄弟链用 output_2→input_2 定序
        var isLinear = def.type === 'treeSequence' || def.type === 'treeLoop';

        var prevChildId = null;
        for (var i = 0; i < children.length; i++) {
            var childParent = isLinear ? (i === 0 ? dfId : null) : dfId;
            var childId = walk(children[i], childParent);

            // 兄弟链：前一个 output_2 → 当前 input_2
            if (prevChildId && childId) {
                try { editor.addConnection(prevChildId, childId, 'output_2', 'input_2'); } catch(e) {}
            }
            prevChildId = childId;
        }

        return dfId;
    }

    walk(data.TreeRoot, null);

    // 恢复 zoom
    if (data._drawflow && data._drawflow.zoom) {
        // Drawflow 不支持直接设置 zoom 值，通过 import 保留
    }

    isDirty = false;
    updateInfo();
    updateFooter();
}

function autoLayout(rootData) {
    var layout = {};
    var yOffsets = {}; // depth → next y position
    var xStep = 280;
    var yStep = 90;
    var xStart = 80;

    function walk(node, depth) {
        if (!yOffsets[depth]) yOffsets[depth] = 80;
        var x = xStart + depth * xStep;
        var y = yOffsets[depth];
        layout[node.Id] = [x, y];
        yOffsets[depth] += yStep;

        var children = node.Childs || [];
        for (var i = 0; i < children.length; i++) {
            walk(children[i], depth + 1);
        }
    }

    walk(rootData, 0);
    return layout;
}

function exportTreeFromDrawflow() {
    var dfData = editor.export();
    var nodes = dfData.drawflow.Home.data;
    if (!nodes || Object.keys(nodes).length === 0) {
        return buildEmptyAxis();
    }

    var visited = new Set();
    var positions = {};

    // 找到 Root 节点（没有 input 连接）
    var rootDfId = null;
    for (var id in nodes) {
        var ndf = getNodeDef(nodes[id].name);
        if (ndf && ndf.type === 'treeRoot') {
            rootDfId = id;
            break;
        }
    }
    if (!rootDfId) {
        // 找第一个没有输入连接的节点作为 root
        for (var id in nodes) {
            var n = nodes[id];
            var hasInput = false;
            if (n.inputs) {
                for (var ik in n.inputs) {
                    if (n.inputs[ik].connections && n.inputs[ik].connections.length > 0) {
                        hasInput = true; break;
                    }
                }
            }
            if (!hasInput) { rootDfId = id; break; }
        }
    }

    if (!rootDfId) return buildEmptyAxis();

    function buildTreeNode(dfId) {
        if (visited.has(dfId)) return null;
        visited.add(dfId);

        var node = nodes[dfId];
        var treeData = dfIdToData[dfId];
        if (!treeData) {
            var ndef = getNodeDef(node.name);
            treeData = {
                '$type': TYPE_FULL[ndef ? ndef.type : 'treeSequence'] || node.name,
                DisplayName: ndef ? ndef.label : node.name,
                Id: parseInt(dfId),
                Enable: true, Remark: '', Tag: '', Childs: []
            };
        } else {
            treeData = JSON.parse(JSON.stringify(treeData));
        }
        treeData.Childs = [];

        // 保存位置
        positions[treeData.Id] = [node.pos_x, node.pos_y];

        // 获取所有子节点（通过 output_1，父子关系）
        var childIds = [];
        if (node.outputs && node.outputs.output_1) {
            (node.outputs.output_1.connections || []).forEach(function(c) {
                childIds.push(c.node);
            });
        }

        if (childIds.length === 0) return treeData;

        // 通过 output_2→input_2 兄弟链确定子节点顺序
        var ordered = orderBySiblingChain(childIds, nodes);

        for (var i = 0; i < ordered.length; i++) {
            var child = buildTreeNode(ordered[i]);
            if (child) treeData.Childs.push(child);
        }

        return treeData;
    }

    // output_2→input_2 兄弟链定序
    function orderBySiblingChain(childIds, nodes) {
        if (childIds.length <= 1) return childIds;
        var childSet = {};
        childIds.forEach(function(id) { childSet[id] = true; });

        // 找第一个（没有来自集合内的 input_2 连接）
        var first = null;
        childIds.forEach(function(cid) {
            var cn = nodes[cid];
            if (!cn || !cn.inputs || !cn.inputs.input_2) { if (!first) first = cid; return; }
            var fromSibling = (cn.inputs.input_2.connections || []).some(function(c) {
                return childSet[c.node];
            });
            if (!fromSibling) first = cid;
        });
        if (!first) return childIds;

        var ordered = [first];
        var seen = {};
        seen[first] = true;
        var cur = first;

        while (cur) {
            var cn = nodes[cur];
            var next = null;
            if (cn && cn.outputs && cn.outputs.output_2) {
                (cn.outputs.output_2.connections || []).forEach(function(c) {
                    if (childSet[c.node] && !seen[c.node]) {
                        next = c.node; seen[c.node] = true;
                    }
                });
            }
            if (next) { ordered.push(next); cur = next; }
            else break;
        }

        // 追回断链的
        childIds.forEach(function(cid) {
            if (!seen[cid]) ordered.push(cid);
        });
        return ordered;
    }

    var rootNode = buildTreeNode(rootDfId);
    if (!rootNode) return buildEmptyAxis();

    var wrapper = AXIS_DATA[currentAxis] || { Name: '新执行轴', TerritoryTypeId: 0, Note: '', ExposedVars: [] };

    return {
        Name: wrapper.Name || '新执行轴',
        TerritoryTypeId: wrapper.TerritoryTypeId || 0,
        Note: wrapper.Note || '',
        ExposedVars: wrapper.ExposedVars || [],
        TreeRoot: rootNode,
        _drawflow: {
            positions: positions,
            zoom: editor.zoom
        }
    };
}

function buildEmptyAxis() {
    return {
        Name: AXIS_DATA[currentAxis] ? AXIS_DATA[currentAxis].Name : '新执行轴',
        TerritoryTypeId: 0, Note: '', ExposedVars: [],
        TreeRoot: newNodeDefaults('treeRoot')
    };
}

// ====== Part 8: File Operations ======

function newFile() {
    if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    var rootData = newNodeDefaults('treeRoot');
    var treeData = {
        Name: '新执行轴', TerritoryTypeId: 0, Note: '', ExposedVars: [],
        TreeRoot: rootData
    };
    AXIS_DATA[currentAxis] = { Name: '新执行轴', TerritoryTypeId: 0, Note: '', ExposedVars: [] };
    currentFile = ''; fileHandle = null;
    importTreeToDrawflow(treeData);
    isDirty = true;
    updateFooter();
    setStatus('已新建', 'ok');
}

async function loadFile() {
    if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    if (window.showOpenFilePicker) {
        try {
            var handles = await window.showOpenFilePicker({
                types: [{ description: 'JSON', accept: { 'application/json': ['.json', '.txt'] } }]
            });
            var file = await handles[0].getFile();
            fileHandle = handles[0];
            readFileObj(file);
        } catch(e) { if (e.name !== 'AbortError') setStatus('加载失败', 'err'); }
    } else {
        document.getElementById('edFileInput').click();
    }
}

function readFileObj(file) {
    var reader = new FileReader();
    reader.onload = function() {
        try {
            var data = JSON.parse(reader.result);
            currentFile = file.name;
            importTreeToDrawflow(data);
            isDirty = false;
            updateFooter();
            setStatus('已加载: ' + file.name, 'ok');
        } catch(ex) {
            setStatus('JSON 解析失败: ' + ex.message, 'err');
        }
    };
    reader.readAsText(file);
}

async function saveFile() {
    var data = exportTreeFromDrawflow();
    if (!data) { setStatus('无内容', 'err'); return; }
    var json = JSON.stringify(data, null, 2);
    if (fileHandle && typeof fileHandle.createWritable === 'function') {
        try {
            var w = await fileHandle.createWritable();
            await w.write(json);
            await w.close();
            isDirty = false;
            updateFooter();
            setStatus('已保存: ' + currentFile, 'ok');
            return;
        } catch(e) {}
    }
    saveFileAs();
}

async function saveFileAs() {
    var data = exportTreeFromDrawflow();
    if (!data) { setStatus('无内容', 'err'); return; }
    var json = JSON.stringify(data, null, 2);
    var name = currentFile || 'timeline.json';
    if (window.showSaveFilePicker) {
        try {
            var h = await window.showSaveFilePicker({
                suggestedName: name,
                types: [{ description: 'JSON', accept: { 'application/json': ['.json'] } }]
            });
            var w = await h.createWritable();
            await w.write(json);
            await w.close();
            fileHandle = h;
            currentFile = h.name;
            isDirty = false;
            updateFooter();
            setStatus('已保存: ' + currentFile, 'ok');
            return;
        } catch(e) { if (e.name === 'AbortError') return; }
    }
    downloadJson(json, name);
}

function exportFile() {
    var data = exportTreeFromDrawflow();
    if (!data) { setStatus('无内容', 'err'); return; }
    downloadJson(JSON.stringify(data, null, 2), currentFile || 'export.json');
    setStatus('已导出', 'ok');
}

function downloadJson(json, name) {
    var blob = new Blob([json], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url; a.download = name;
    a.click();
    URL.revokeObjectURL(url);
}

// Directory picker
async function pickDirectory() {
    if (!window.showDirectoryPicker) { setStatus('浏览器不支持目录选择', 'err'); return; }
    try {
        dirHandle = await window.showDirectoryPicker();
        refreshFileList();
    } catch(e) { if (e.name !== 'AbortError') setStatus('目录选择失败', 'err'); }
}

async function refreshFileList() {
    // Palette panel 底部可加文件列表，这里保留接口
    if (!dirHandle) return;
    fileEntries = [];
    for await (var entry of dirHandle.values()) {
        if (entry.kind === 'file' && entry.name.endsWith('.json')) {
            fileEntries.push({ name: entry.name, handle: entry });
        }
    }
    fileEntries.sort(function(a, b) { return a.name.localeCompare(b.name); });
}

async function openFromDir(fileName) {
    var entry = fileEntries.find(function(e) { return e.name === fileName; });
    if (!entry) return;
    try {
        var file = await entry.handle.getFile();
        fileHandle = entry.handle;
        readFileObj(file);
    } catch(e) { setStatus('打开失败: ' + e.message, 'err'); }
}

// ====== Part 9: Property Panel ======

function renderProps() {
    var body = document.getElementById('propsBody');
    var hdr = document.getElementById('propsLabel');

    if (!selectedDfId || !dfIdToData[selectedDfId]) {
        hdr.textContent = '节点属性';
        body.innerHTML = '<div class="props-hint">点击节点查看属性</div>';
        return;
    }

    var node = dfIdToData[selectedDfId];
    var def = getNodeDefByData(node);
    hdr.textContent = '节点: ' + (def ? def.label : '?');

    var h = '<div class="prop-section"><div class="prop-head">基础</div>';
    h += propRow('名称', 'text', 'DisplayName', node.DisplayName || '');
    h += propRow('ID', 'number', 'Id', node.Id);
    h += propRow('启用', 'checkbox', 'Enable', node.Enable);
    h += propRow('备注', 'text', 'Remark', node.Remark || '');
    h += propRow('标签', 'text', 'Tag', node.Tag || '');
    h += '</div>';

    if (def.type === 'treeCondNode') {
        h += '<div class="prop-section"><div class="prop-head">条件</div>';
        h += propRow('检查一次', 'checkbox', 'CheckOnce', node.CheckOnce);
        h += propRow('结果取反', 'checkbox', 'ReverseResult', node.ReverseResult);
        h += '<div style="font-size:11px;color:var(--tx2);padding:4px 0">条件列表: ' + ((node.TriggerConds || []).length) + ' 项</div>';
        h += '</div>';
    }
    if (def.type === 'treeActionNode') {
        h += '<div class="prop-section"><div class="prop-head">动作</div>';
        h += '<div style="font-size:11px;color:var(--tx2);padding:4px 0">动作列表: ' + ((node.TriggerActions || []).length) + ' 项</div>';
        h += '</div>';
    }
    if (def.type === 'treeDelayNode') {
        h += '<div class="prop-section"><div class="prop-head">延迟</div>';
        h += propRow('秒数', 'number', 'Delay', node.Delay);
        h += '</div>';
    }
    if (def.type === 'treeLoop') {
        h += '<div class="prop-section"><div class="prop-head">循环</div>';
        h += propRow('次数', 'number', 'Times', node.Times);
        h += '</div>';
    }
    if (def.type === 'treeScriptNode') {
        h += '<div class="prop-section"><div class="prop-head">脚本</div>';
        h += '<textarea class="prop-area" id="propScript" style="height:80px">' + esc(node.Script || '') + '</textarea>';
        h += '<button class="btn" style="margin-top:4px;font-size:11px" id="btnSaveScript">保存脚本</button>';
        h += '</div>';
    }
    if (def.type === 'treeSequence') {
        h += '<div class="prop-section">';
        h += propRow('忽略结果', 'checkbox', 'IgnoreNodeResult', node.IgnoreNodeResult);
        h += '</div>';
    }
    if (def.type === 'treeParallel') {
        h += '<div class="prop-section">';
        h += propRow('任一返回', 'checkbox', 'AnyReturn', node.AnyReturn);
        h += '</div>';
    }

    body.innerHTML = h;
    bindPropInputs();
}

function propRow(label, type, key, value) {
    var id = 'prop_' + key;
    if (type === 'checkbox') {
        return '<div class="prop-row"><span class="prop-label">' + label + '</span>' +
            '<input class="prop-input" type="checkbox" id="' + id + '" data-key="' + key + '" ' + (value ? 'checked' : '') + '></div>';
    }
    if (type === 'number') {
        return '<div class="prop-row"><span class="prop-label">' + label + '</span>' +
            '<input class="prop-input" type="number" id="' + id + '" data-key="' + key + '" value="' + (value !== undefined ? value : '') + '" style="width:80px"></div>';
    }
    return '<div class="prop-row"><span class="prop-label">' + label + '</span>' +
        '<input class="prop-input" type="text" id="' + id + '" data-key="' + key + '" value="' + esc(String(value || '')) + '"></div>';
}

function bindPropInputs() {
    document.querySelectorAll('#propsBody .prop-input').forEach(function(el) {
        if (el.dataset.bound) return;
        el.dataset.bound = '1';
        el.addEventListener('change', function() {
            if (!selectedDfId || !dfIdToData[selectedDfId]) return;
            var key = this.dataset.key;
            var val = this.type === 'checkbox' ? this.checked :
                      (this.type === 'number' ? Number(this.value) : this.value);
            dfIdToData[selectedDfId][key] = val;
            updateNodeView(selectedDfId);
            markDirty();
        });
    });

    var btnSaveScript = document.getElementById('btnSaveScript');
    if (btnSaveScript) {
        btnSaveScript.addEventListener('click', function() {
            if (!selectedDfId || !dfIdToData[selectedDfId]) return;
            var ta = document.getElementById('propScript');
            if (ta) { dfIdToData[selectedDfId].Script = ta.value; markDirty(); setStatus('脚本已保存', 'ok'); }
        });
    }
}

// ====== Part 10: Context Menu & Keyboard ======

function showContextMenu(ev) {
    hideContextMenu();

    var menu = document.createElement('div');
    menu.className = 'ctx-menu';
    menu.id = 'ctxMenu';
    menu.style.left = ev.clientX + 'px';
    menu.style.top = ev.clientY + 'px';

    // 判断点击位置是否在某个节点上
    var targetNode = null;
    var targetDfId = null;

    // 查找最近的 drawflow-node
    var el = ev.target;
    while (el) {
        if (el.classList.contains('drawflow-node') && el.id && el.id.indexOf('node-') === 0) {
            targetDfId = parseInt(el.id.replace('node-', ''));
            var data = dfIdToData[targetDfId];
            if (data) {
                var def = getNodeDefByData(data);
                if (def) targetNode = { dfId: targetDfId, data: data, def: def };
            }
            break;
        }
        el = el.parentElement;
    }

    if (targetNode && targetNode.def.composite) {
        // 复合节点 → 显示添加子节点菜单
        var addLabel = document.createElement('div');
        addLabel.className = 'ctx-item';
        addLabel.style.fontWeight = '600';
        addLabel.innerHTML = '<span style="font-size:11px;color:var(--tx2)">添加子节点:</span>';
        addLabel.style.pointerEvents = 'none';
        menu.appendChild(addLabel);

        NODE_DEFS.forEach(function(ndef) {
            if (ndef.type === 'treeRoot') return;
            var item = document.createElement('div');
            item.className = 'ctx-item';
            item.innerHTML = '<span class="palette-dot" style="display:inline-block;background:' + ndef.color + ';width:10px;height:10px;border-radius:50%"></span>' + ndef.label;
            item.addEventListener('click', function() {
                addChildToNode(targetNode.dfId, ndef.type, ev.clientX, ev.clientY);
            });
            menu.appendChild(item);
        });

        var sep = document.createElement('div');
        sep.className = 'ctx-sep';
        menu.appendChild(sep);

        var delItem = document.createElement('div');
        delItem.className = 'ctx-item ctx-danger';
        delItem.textContent = '✕ 删除节点';
        delItem.addEventListener('click', function() {
            deleteDrawflowNode(targetNode.dfId);
        });
        menu.appendChild(delItem);

    } else if (targetNode) {
        // 普通节点 → 删除
        var delItem = document.createElement('div');
        delItem.className = 'ctx-item ctx-danger';
        delItem.textContent = '✕ 删除节点';
        delItem.addEventListener('click', function() {
            deleteDrawflowNode(targetNode.dfId);
        });
        menu.appendChild(delItem);
    } else {
        // 空画布 → 添加节点
        var addLabel = document.createElement('div');
        addLabel.className = 'ctx-item';
        addLabel.style.fontWeight = '600';
        addLabel.innerHTML = '<span style="font-size:11px;color:var(--tx2)">添加节点:</span>';
        addLabel.style.pointerEvents = 'none';
        menu.appendChild(addLabel);

        NODE_DEFS.forEach(function(ndef) {
            if (ndef.type === 'treeRoot') return;
            var item = document.createElement('div');
            item.className = 'ctx-item';
            item.innerHTML = '<span class="palette-dot" style="display:inline-block;background:' + ndef.color + ';width:10px;height:10px;border-radius:50%"></span>' + ndef.label;
            item.addEventListener('click', function() {
                addNodeAtPosition(ndef.type, ev.clientX, ev.clientY);
            });
            menu.appendChild(item);
        });
    }

    document.body.appendChild(menu);
    ev.stopPropagation();
}

function hideContextMenu() {
    var m = document.getElementById('ctxMenu');
    if (m) m.remove();
}

function addChildToNode(parentDfId, childType, clientX, clientY) {
    var def = getNodeDef(childType);
    var data = newNodeDefaults(childType);
    var canvas = editor.precanvas;
    var rect = canvas.getBoundingClientRect();
    var posX = (clientX - rect.left) / editor.zoom;
    var posY = (clientY - rect.top) / editor.zoom;
    var childDfId = createDrawflowNode(def, data, posX, posY);
    connectAsLastChild(parentDfId, childDfId);
    hideContextMenu();
}

// 连接父子关系 + 插入兄弟链末尾
function connectAsLastChild(parentDfId, childDfId) {
    // 父子连线：父 output_1 → 子 input_1
    try { editor.addConnection(parentDfId, childDfId, 'output_1', 'input_1'); } catch(e) {}

    // 兄弟链：找最后一个兄弟，其 output_2 → 新子 input_2
    var lastSibling = findLastSiblingInChain(parentDfId, childDfId);
    if (lastSibling) {
        try { editor.addConnection(lastSibling, childDfId, 'output_2', 'input_2'); } catch(e) {}
    }
}

// 找兄弟链最末节点（output_2→input_2 链）
function findLastSiblingInChain(parentDfId, excludeDfId) {
    var dfData = editor.export();
    var nodes = dfData.drawflow.Home.data;
    var parentNode = nodes[parentDfId];
    if (!parentNode || !parentNode.outputs || !parentNode.outputs.output_1) return null;

    // 收集所有子节点（output_1 连接，排除自身）
    var childSet = {};
    (parentNode.outputs.output_1.connections || []).forEach(function(c) {
        if (String(c.node) !== String(excludeDfId)) childSet[c.node] = true;
    });
    var childIds = Object.keys(childSet);
    if (childIds.length === 0) return null;

    // 找第一个（没有来自集合内的 input_2）
    var first = null;
    childIds.forEach(function(cid) {
        var cn = nodes[cid];
        if (!cn || !cn.inputs || !cn.inputs.input_2) { if (!first) first = cid; return; }
        var fromSib = (cn.inputs.input_2.connections || []).some(function(c) {
            return childSet[c.node];
        });
        if (!fromSib) first = cid;
    });
    if (!first) return null;

    // 沿 output_2 走到末尾
    var cur = first;
    while (true) {
        var cn = nodes[cur];
        var next = null;
        if (cn && cn.outputs && cn.outputs.output_2) {
            (cn.outputs.output_2.connections || []).forEach(function(c) {
                if (childSet[c.node]) next = c.node;
            });
        }
        if (next) cur = next;
        else return cur;
    }
}

function addNodeAtPosition(nodeType, clientX, clientY) {
    var def = getNodeDef(nodeType);
    var data = newNodeDefaults(nodeType);
    var canvas = editor.precanvas;
    var rect = canvas.getBoundingClientRect();
    var posX = (clientX - rect.left) / editor.zoom;
    var posY = (clientY - rect.top) / editor.zoom;
    createDrawflowNode(def, data, posX, posY);
    hideContextMenu();
}

function deleteDrawflowNode(dfId) {
    hideContextMenu();
    if (dfIdToData[dfId]) {
        var def = getNodeDefByData(dfIdToData[dfId]);
        if (def && def.type === 'treeRoot') {
            setStatus('不能删除根节点', 'err');
            return;
        }
    }
    editor.removeNodeId('node-' + dfId);
    delete dfIdToData[dfId];
    if (selectedDfId === dfId) { selectedDfId = null; renderProps(); }
    markDirty();
}

function initKeyboard() {
    document.addEventListener('keydown', function(e) {
        // Ctrl+S
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            saveFile();
            return;
        }
        // Delete / Backspace 删除选中节点
        if ((e.key === 'Delete' || e.key === 'Backspace') && selectedDfId) {
            var activeEl = document.activeElement;
            if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA' || activeEl.contentEditable === 'true')) return;
            e.preventDefault();
            deleteDrawflowNode(selectedDfId);
        }
    });

    document.addEventListener('click', function(e) {
        if (!e.target.closest('.ctx-menu')) {
            hideContextMenu();
        }
    });
}

// ====== Part 11: Utilities ======

function getNodeDef(type) {
    return NODE_DEFS.find(function(d) { return d.type === type; });
}

function getNodeDefByData(nodeData) {
    if (!nodeData) return null;
    var tn = (nodeData['$type'] || '').split(',').pop();
    var typeMap = {
        'TreeRoot': 'treeRoot', 'TreeSequence': 'treeSequence',
        'TreeParallel': 'treeParallel', 'TreeSelect': 'treeSelect',
        'TreeLoop': 'treeLoop', 'TreeCondNode': 'treeCondNode',
        'TreeActionNode': 'treeActionNode', 'TreeDelayNode': 'treeDelayNode',
        'TreeScriptNode': 'treeScriptNode'
    };
    var type = typeMap[tn];
    if (type) return NODE_DEFS.find(function(d) { return d.type === type; });
    // Fallback: 从数据结构推断
    if (nodeData.Times !== undefined) return NODE_DEFS.find(function(d) { return d.type === 'treeLoop'; });
    if (nodeData.Delay !== undefined && !nodeData.Childs) return NODE_DEFS.find(function(d) { return d.type === 'treeDelayNode'; });
    if (nodeData.TriggerConds) return NODE_DEFS.find(function(d) { return d.type === 'treeCondNode'; });
    if (nodeData.TriggerActions) return NODE_DEFS.find(function(d) { return d.type === 'treeActionNode'; });
    if (nodeData.Script !== undefined) return NODE_DEFS.find(function(d) { return d.type === 'treeScriptNode'; });
    if (nodeData.Childs) return NODE_DEFS.find(function(d) { return d.type === 'treeSequence'; });
    return NODE_DEFS.find(function(d) { return d.type === 'treeSequence'; });
}

function newNodeDefaults(type) {
    var def = getNodeDef(type);
    autoIdCounter++;
    var d = {
        '$type': TYPE_FULL[type] || type,
        DisplayName: def ? def.label : type,
        Id: autoIdCounter,
        Enable: true,
        Remark: '',
        Tag: ''
    };
    if (def && def.composite) d.Childs = [];
    if (type === 'treeLoop') d.Times = 1;
    if (type === 'treeDelayNode') d.Delay = 0;
    if (type === 'treeCondNode') { d.CheckOnce = false; d.ReverseResult = false; d.TriggerConds = []; }
    if (type === 'treeActionNode') d.TriggerActions = [];
    if (type === 'treeScriptNode') { d.Script = ''; d.OnlyCheck = false; }
    if (type === 'treeParallel') d.AnyReturn = false;
    if (type === 'treeSequence') d.IgnoreNodeResult = false;
    return d;
}

function markDirty() { isDirty = true; updateInfo(); updateFooter(); }

function updateInfo() {
    document.getElementById('infoType').textContent = currentAxis === 'execution' ? '执行轴' : '辅助轴';
    document.getElementById('infoFile').textContent = currentFile || '—';
    document.getElementById('infoCount').textContent = Object.keys(dfIdToData).length;
    var dEl = document.getElementById('infoDirty');
    dEl.textContent = isDirty ? '已修改' : '已保存';
    dEl.style.color = isDirty ? '#ff9f0a' : '#30d158';
}

function updateFooter() {
    var labels = { execution: '执行轴', assist: '辅助轴' };
    var l = labels[currentAxis] || '';
    document.getElementById('edFooter').textContent =
        (currentFile ? l + ' — ' + currentFile + (isDirty ? ' (未保存)' : '') : '就绪' + (isDirty ? ' (未保存)' : ''));
}

function setStatus(msg, type) {
    var el = document.getElementById('edStatus');
    el.textContent = msg;
    el.className = 'status-text ' + (type || '');
    if (type === 'ok') {
        setTimeout(function() { if (el.textContent === msg) { el.textContent = ''; el.className = 'status-text'; } }, 3000);
    }
}

function esc(s) {
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function deepClone(obj) {
    return JSON.parse(JSON.stringify(obj));
}
