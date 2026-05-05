// ============================================================
// HiAuRo 三轴编辑器
// ============================================================

var currentAxis = 'execution';
var currentFile = '';
var fileHandle = null;
var timelineData = null;
var isDirty = false;
var selectedNodePath = null;
var contextMenuPath = null;
var globalDragBound = false;
var dragState = { active:false, srcPath:null, startX:0, startY:0, moved:false, preventClick:false };

var NODE_STYLES = {
    treeRoot:       { label:'Root',       color:'#30b0ff' },
    treeSequence:   { label:'序列',       color:'#30d158' },
    treeParallel:   { label:'并行',       color:'#ff9f0a' },
    treeSelect:     { label:'选择',       color:'#bf5af2' },
    treeLoop:       { label:'循环',       color:'#ff375f' },
    treeCondNode:   { label:'条件',       color:'#15aabf' },
    treeActionNode: { label:'动作',       color:'#f06595' },
    treeDelayNode:  { label:'延迟',       color:'#fcc419' },
    treeScriptNode: { label:'脚本',       color:'#4dabf7' },
    treePrintNode:  { label:'调试输出',   color:'#868e96' },
    treeClearWait:  { label:'清除等待',   color:'#495057' }
};

var ADD_TYPES = ['treeSequence','treeParallel','treeSelect','treeLoop','treeCondNode','treeActionNode','treeDelayNode','treeScriptNode','treePrintNode','treeClearWait'];

var dirHandle = null;
var fileEntries = [];

// ==================== 初始化 ====================

document.addEventListener('DOMContentLoaded', function() {
    document.querySelectorAll('.tab').forEach(function(btn) {
        btn.addEventListener('click', function() {
            if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
            document.querySelectorAll('.tab').forEach(function(b) { b.classList.remove('active'); });
            btn.classList.add('active');
            currentAxis = btn.dataset.axis;
            switchAxis();
        });
    });
    document.getElementById('btnNew').addEventListener('click', newFile);
    document.getElementById('btnNewBig').addEventListener('click', newFile);
    document.getElementById('btnLoad').addEventListener('click', loadFile);
    document.getElementById('btnLoadBig').addEventListener('click', loadFile);
    document.getElementById('btnSave').addEventListener('click', saveFile);
    document.getElementById('btnSaveAs').addEventListener('click', saveFileAs);
    document.getElementById('btnExport').addEventListener('click', exportFile);
    document.getElementById('btnImport').addEventListener('click', function() { document.getElementById('edFileInput').click(); });
    document.getElementById('edFileInput').addEventListener('change', function() {
        var f = this.files[0]; if (f) { readFileObj(f); this.value = ''; }
    });
    document.getElementById('btnPickDir').addEventListener('click', pickDirectory);
    // 快捷添加按钮
    document.querySelectorAll('.qbtn').forEach(function(btn) {
        btn.addEventListener('click', function() {
            if (!selectedNodePath) { setStatus('请先选中一个父节点','error'); return; }
            var parent = getNodeByPath(selectedNodePath);
            if (!parent) return;
            var type = detectType(parent);
            var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
            if (!isComp) { setStatus('所选节点不支持添加子节点','error'); return; }
            addChildNode(selectedNodePath, this.dataset.add);
        });
    });
    document.addEventListener('keydown', function(e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); saveFile(); }
    });
    document.addEventListener('click', function(e) {
        hideContextMenu();
        if (!e.target.closest('.cust-select')) {
            document.querySelectorAll('.cust-select.open').forEach(function(w) { w.classList.remove('open'); });
        }
    });
    updateFooter();
});

// ==================== 轴切换 ====================

function switchAxis() {
    document.getElementById('treePanel').style.display = 'flex';
    document.getElementById('treeTools').style.display = '';
    selectedNodePath = null;
    renderTree();
    renderProps();
    updateFooter();
}

// ==================== 手写树 (执行轴) ====================

function renderTree() {
    var treeEl = document.getElementById('etree');
    var emptyEl = document.getElementById('emptyTree');
    if (!timelineData) {
        treeEl.style.display = 'none';
        emptyEl.style.display = '';
        document.getElementById('propsBody').innerHTML = '<div class="props-hint">点击节点查看属性</div>';
        return;
    }
    emptyEl.style.display = 'none';
    treeEl.style.display = '';
    var root = timelineData.TreeRoot || timelineData.treeRoot;
    treeEl.innerHTML = root ? buildTreeNodeHtml(root, '', 0) : '<div class="tn-row">空</div>';
    bindTreeClicks();
}

function detectType(node) {
    var full = (node['$type'] || '').split(',')[0];
    var tn = full.split('.').pop();
    var map = { TreeRoot:'treeRoot', TreeSequence:'treeSequence', TreeParallel:'treeParallel', TreeSelect:'treeSelect', TreeLoop:'treeLoop', TreeCondNode:'treeCondNode', TreeActionNode:'treeActionNode', TreeDelayNode:'treeDelayNode', TreeScriptNode:'treeScriptNode', TreePrintDebugInfoNode:'treePrintNode', TreeClearWaitNode:'treeClearWait' };
    if (map[tn]) return map[tn];
    if (node.TriggerConds) return 'treeCondNode';
    if (node.TriggerActions) return 'treeActionNode';
    if (node.Delay !== undefined) return 'treeDelayNode';
    if (node.Childs) return 'treeSequence';
    return 'treeSequence';
}

function typeToFull(type) {
    var map = { treeRoot:'AEAssist.Trigger.TriggerNode.TreeRoot, AEAssist', treeSequence:'AEAssist.Trigger.TriggerNode.TreeSequence, AEAssist', treeParallel:'AEAssist.Trigger.TriggerNode.TreeParallel, AEAssist', treeSelect:'AEAssist.Trigger.TriggerNode.TreeSelect, AEAssist', treeLoop:'AEAssist.Trigger.TriggerNode.TreeLoop, AEAssist', treeCondNode:'AEAssist.Trigger.TriggerNode.TreeCondNode, AEAssist', treeActionNode:'AEAssist.Trigger.TriggerNode.TreeActionNode, AEAssist', treeDelayNode:'AEAssist.Trigger.TriggerNode.TreeDelayNode, AEAssist', treeScriptNode:'AEAssist.Trigger.TriggerNode.TreeScriptNode, AEAssist', treePrintNode:'AEAssist.Trigger.TriggerNode.TreePrintDebugInfoNode, AEAssist', treeClearWait:'AEAssist.Trigger.TriggerNode.TreeClearWaitNode, AEAssist' };
    return map[type] || type;
}

function newNodeDefaults(type) {
    var d = { '$type': typeToFull(type), DisplayName: NODE_STYLES[type].label, Id: Date.now(), Enable: true, Remark: '', Tag: '' };
    var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
    if (isComp) d.Childs = [];
    if (type === 'treeLoop') d.Times = 1;
    if (type === 'treeDelayNode') d.Delay = 0;
    if (type === 'treeCondNode') { d.CheckOnce = false; d.ReverseResult = false; d.TriggerConds = []; }
    if (type === 'treeActionNode') d.TriggerActions = [];
    if (type === 'treeScriptNode') { d.Script = ''; d.OnlyCheck = false; }
    if (type === 'treeParallel') d.AnyReturn = false;
    if (type === 'treeSequence') d.IgnoreNodeResult = false;
    return d;
}

function buildTreeNodeHtml(node, path, depth) {
    var type = detectType(node);
    var st = NODE_STYLES[type] || NODE_STYLES.treeSequence;
    var label = node.DisplayName || node.name || st.label;
    var nodePath = path + (path ? '_' : '') + 'n';
    var selClass = nodePath === selectedNodePath ? ' sel' : '';
    var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
    var children = node.Childs || node.childs || [];

    var h = '<div class="tn-block" data-path="' + nodePath + '">';
    h += '<div class="tn-row' + selClass + '" data-path="' + nodePath + '" data-type="' + type + '" style="border-left:4px solid ' + st.color + '">';
    h += '<span class="tn-tag" style="background:' + st.color + '">' + st.label + '</span>';
    h += '<span class="tn-name">' + esc(label) + '</span>';
    if (isComp) h += '<span class="tn-child-count">' + children.length + ' 子</span>';
    if (isComp) h += '<button class="tn-add" data-add="' + nodePath + '" title="添加子节点">＋</button>';
    if (type !== 'treeRoot') h += '<button class="tn-del" data-del="' + nodePath + '" title="删除节点">✕</button>';
    h += '</div>';

    if (isComp && children.length > 0) {
        h += '<div class="tn-children">';
        children.forEach(function(child, i) {
            h += buildTreeNodeHtml(child, nodePath + '_' + i, depth + 1);
        });
        h += '</div>';
    }
    h += '</div>';
    return h;
}

function bindTreeClicks() {
    var tree = document.getElementById('etree');
    if (!tree) return;

    tree.querySelectorAll('.tn-row').forEach(function(row) {
        row.addEventListener('click', function(e) {
            if (dragState.preventClick) return; // 拖拽后忽略 click
            if (e.target.closest('.tn-add') || e.target.closest('.tn-del')) return;
            selectedNodePath = this.dataset.path;
            document.querySelectorAll('.tn-row').forEach(function(r) { r.classList.remove('sel'); });
            this.classList.add('sel');
            renderProps();
        });

        row.addEventListener('contextmenu', function(e) {
            e.preventDefault();
            e.stopPropagation();
            showContextMenu(e, this.dataset.path);
        });

        // 手动拖拽 (左键按住 + 移动), 避免 HTML5 drag 干扰右键
        row.addEventListener('mousedown', function(e) {
            if (e.button !== 0) return;                     // 仅左键
            if (this.dataset.path === 'n') return;           // 根节点不可拖
            if (e.target.closest('.tn-add') || e.target.closest('.tn-del')) return;
            dragState.active = true;
            dragState.srcPath = this.dataset.path;
            dragState.startX = e.clientX;
            dragState.startY = e.clientY;
            dragState.moved = false;
        });
    });

    // 全局拖拽事件 (仅注册一次)
    if (!globalDragBound) {
        globalDragBound = true;

        document.addEventListener('mousemove', function(e) {
            if (!dragState.active || dragState.moved) return;
            var dx = Math.abs(e.clientX - dragState.startX);
            var dy = Math.abs(e.clientY - dragState.startY);
            if (dx + dy < 4) return;

            dragState.moved = true;
            document.body.classList.add('drag-active');
            var srcRow = document.querySelector('.tn-row[data-path="' + dragState.srcPath + '"]');
            if (srcRow) srcRow.classList.add('dragging');
        });

        document.addEventListener('mouseup', function(e) {
            if (!dragState.active) return;
            var srcPath = dragState.srcPath;
            var moved = dragState.moved;
            dragState.active = false;
            dragState.moved = false;
            document.body.classList.remove('drag-active');
            var srcRow = document.querySelector('.tn-row[data-path="' + srcPath + '"]');
            if (srcRow) srcRow.classList.remove('dragging');

            if (moved) {
                dragState.preventClick = true;
                setTimeout(function() { dragState.preventClick = false; }, 50);
                var target = document.elementFromPoint(e.clientX, e.clientY);
                var dstRow = target ? target.closest('.tn-row') : null;
                var dir = 'above';
                if (dstRow) {
                    if (dstRow.classList.contains('drop-child')) dir = 'child';
                    else if (dstRow.classList.contains('drop-below')) dir = 'below';
                }
                clearDropIndicators();
                if (!dstRow || dstRow.dataset.path === dragState.srcPath) return;
                // 防止拖入自己的后代
                if (dstRow.dataset.path.indexOf(dragState.srcPath + '_') === 0) return;
                moveNode(dragState.srcPath, dstRow.dataset.path, dir);
            }
        });

        // 拖拽过程中的放置指示器 (三区: 上边缘=前插, 中间复合节点=加入子, 下边缘=后插)
        tree.addEventListener('mousemove', function(e) {
            if (!dragState.moved) return;
            clearDropIndicators();
            var target = document.elementFromPoint(e.clientX, e.clientY);
            var row = target ? target.closest('.tn-row') : null;
            if (!row || row.dataset.path === dragState.srcPath) return;
            // 排除拖到自己的后代中
            if (row.dataset.path.indexOf(dragState.srcPath + '_') === 0) return;
            // Root 只能作为子节点添加
            if (row.dataset.path === 'n') { row.classList.add('drop-child'); return; }
            var rect = row.getBoundingClientRect();
            var zone = (e.clientY - rect.top) / rect.height;
            var type = row.dataset.type;
            var isComp = type && ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
            if (zone < 0.25) {
                row.classList.add('drop-above');
            } else if (zone > 0.75) {
                row.classList.add('drop-below');
            } else if (isComp) {
                row.classList.add('drop-child');
            } else {
                // 非复合节点中间区域: 仍按中线分上/下
                row.classList.add(zone < 0.5 ? 'drop-above' : 'drop-below');
            }
        });
    }

    tree.querySelectorAll('.tn-add').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.stopPropagation();
            showAddTypeMenu(e, this.dataset.add);
        });
    });

    tree.querySelectorAll('.tn-del').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.stopPropagation();
            if (!confirm('确定删除此节点？')) return;
            deleteNode(this.dataset.del);
        });
    });
}

function showContextMenu(e, path) {
    hideContextMenu();
    contextMenuPath = path;
    var node = getNodeByPath(path);
    if (!node) return;
    var type = detectType(node);
    var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
    var isRoot = type === 'treeRoot';

    var menu = document.createElement('div');
    menu.className = 'ctx-menu';
    menu.id = 'ctxMenu';
    menu.style.left = e.clientX + 'px';
    menu.style.top = e.clientY + 'px';

    if (isComp) {
        ADD_TYPES.forEach(function(ct) {
            var ns = NODE_STYLES[ct];
            var item = document.createElement('div');
            item.className = 'ctx-item';
            item.innerHTML = '<span class="ctx-dot" style="background:' + ns.color + '"></span>添加 ' + ns.label;
            item.addEventListener('click', function() { addChildNode(path, ct); });
            menu.appendChild(item);
        });
        var sep = document.createElement('div');
        sep.className = 'ctx-sep';
        menu.appendChild(sep);
    }

    if (!isRoot) {
        var upItem = document.createElement('div');
        upItem.className = 'ctx-item';
        upItem.textContent = '⬆ 上移';
        upItem.addEventListener('click', function() { moveSibling(path, -1); });
        menu.appendChild(upItem);

        var downItem = document.createElement('div');
        downItem.className = 'ctx-item';
        downItem.textContent = '⬇ 下移';
        downItem.addEventListener('click', function() { moveSibling(path, 1); });
        menu.appendChild(downItem);

        var sep2 = document.createElement('div');
        sep2.className = 'ctx-sep';
        menu.appendChild(sep2);

        var delItem = document.createElement('div');
        delItem.className = 'ctx-item ctx-danger';
        delItem.textContent = '✕ 删除';
        delItem.addEventListener('click', function() {
            if (confirm('确定删除此节点？')) deleteNode(path);
        });
        menu.appendChild(delItem);
    }

    document.body.appendChild(menu);
    e.stopPropagation();
}

function hideContextMenu() {
    var m = document.getElementById('ctxMenu');
    if (m && m.remove) m.remove();
    contextMenuPath = null;
}

function clearDropIndicators() {
    document.querySelectorAll('.tn-row.drop-above, .tn-row.drop-below, .tn-row.drop-child').forEach(function(r) {
        r.classList.remove('drop-above', 'drop-below', 'drop-child');
    });
}

function showAddTypeMenu(e, parentPath) {
    hideContextMenu();
    var menu = document.createElement('div');
    menu.className = 'ctx-menu';
    menu.id = 'ctxMenu';
    var rect = e.target.getBoundingClientRect();
    menu.style.left = rect.left + 'px';
    menu.style.top = (rect.bottom + 4) + 'px';

    ADD_TYPES.forEach(function(ct) {
        var ns = NODE_STYLES[ct];
        var item = document.createElement('div');
        item.className = 'ctx-item';
        item.innerHTML = '<span class="ctx-dot" style="background:' + ns.color + '"></span>' + ns.label;
        item.addEventListener('click', function() { addChildNode(parentPath, ct); });
        menu.appendChild(item);
    });

    document.body.appendChild(menu);
    e.stopPropagation();
}

function getNodeByPath(path) {
    if (!path || !timelineData) return null;
    var root = timelineData.TreeRoot || timelineData.treeRoot;
    if (!root) return null;
    var parts = path.split('_');
    var obj = root;
    for (var i = 0; i < parts.length; i++) {
        var idx = parseInt(parts[i]);
        if (isNaN(idx)) continue;
        var children = obj.Childs || obj.childs || [];
        if (idx >= children.length) return null;
        obj = children[idx];
    }
    return obj;
}

function getParentInfo(path) {
    // path 格式: n / n_0_n / n_1_n / n_0_n_0_n (n标记与数字索引交替)
    // 最后一个 _n 是节点标记, 倒数第二个是它在父级 children 中的索引
    if (!path) return null;
    var parts = path.split('_');
    if (parts.length < 2) {
        // 根节点 n: 无父级
        var root = timelineData && (timelineData.TreeRoot || timelineData.treeRoot);
        return { node: root, children: root ? (root.Childs || root.childs || []) : [], idx: -1 };
    }
    var idx = parseInt(parts[parts.length - 2]);  // 倒数第二个是数字索引
    var parentPath = parts.slice(0, -2).join('_'); // 去掉最后两段得到父路径
    var parent = getNodeByPath(parentPath) || (timelineData && (timelineData.TreeRoot || timelineData.treeRoot));
    if (!parent) return null;
    return { node: parent, children: parent.Childs || parent.childs || [], idx: idx };
}

function addChildNode(parentPath, type) {
    var parent = getNodeByPath(parentPath);
    if (!parent) return;
    if (!parent.Childs) parent.Childs = [];
    parent.Childs.push(newNodeDefaults(type));
    hideContextMenu();
    markDirty();
}

function deleteNode(path) {
    var p = getParentInfo(path);
    if (!p || !p.children || p.idx < 0) return;
    p.children.splice(p.idx, 1);
    if (selectedNodePath === path || (selectedNodePath||'').indexOf(path + '_') === 0) selectedNodePath = null;
    markDirty();
}

function moveNode(srcPath, dstPath, dir) {
    var srcNode = getNodeByPath(srcPath);
    var dstNode = getNodeByPath(dstPath);
    if (!srcNode || !dstNode) return;
    // 防止拖入自己的后代
    if (dstPath.indexOf(srcPath + '_') === 0) return;

    if (dir === 'child') {
        // 作为子节点添加到目标
        var si = getParentInfo(srcPath);
        if (!si || si.idx < 0 || si.idx >= si.children.length) return;
        if (!dstNode.Childs) dstNode.Childs = [];
        si.children.splice(si.idx, 1);
        dstNode.Childs.push(srcNode);
        markDirty();
        return;
    }

    // sibling 插入 (above / below)
    var si = getParentInfo(srcPath);
    var di = getParentInfo(dstPath);
    if (!si || !di || !si.children || !di.children) return;
    if (si.idx < 0 || si.idx >= si.children.length) return;
    if (di.idx < 0 || di.idx >= di.children.length) return;

    var sameParent = si.children === di.children;
    if (sameParent && si.idx === di.idx) return;

    var item = si.children.splice(si.idx, 1)[0];
    var insertIdx;
    if (sameParent) {
        // 同父级: 源移除后目标索引可能左移
        insertIdx = dir === 'below'
            ? (si.idx < di.idx ? di.idx : di.idx + 1)
            : (si.idx < di.idx ? di.idx - 1 : di.idx);
    } else {
        // 跨父级: di.children 不受源移除影响
        insertIdx = dir === 'below' ? di.idx + 1 : di.idx;
    }
    di.children.splice(insertIdx, 0, item);
    markDirty();
}

function moveSibling(path, delta) {
    var p = getParentInfo(path);
    if (!p || !p.children) return;
    if (p.idx < 0 || p.idx >= p.children.length) return;
    var newIdx = p.idx + delta;
    if (newIdx < 0 || newIdx >= p.children.length) return;
    var item = p.children.splice(p.idx, 1)[0];
    p.children.splice(newIdx, 0, item);
    hideContextMenu();
    markDirty();
}

// ==================== 属性面板 ====================

function renderProps() {
    var body = document.getElementById('propsBody');
    var hdr = document.getElementById('propsLabel');

    if (selectedNodePath && timelineData) {
        var node = getNodeByPath(selectedNodePath);
        if (!node) { body.innerHTML = '<p class="props-hint">节点不存在</p>'; return; }
        var type = detectType(node);
        hdr.textContent = '节点: ' + (node.DisplayName || NODE_STYLES[type].label);
        var h = '<div class="ed-prop-section"><div class="ed-prop-head">基础</div>';
        h += prop('名称', 'text', node, 'DisplayName');
        h += prop('ID', 'number', node, 'Id');
        h += prop('启用', 'checkbox', node, 'Enable');
        h += prop('备注', 'text', node, 'Remark');
        h += prop('标签', 'text', node, 'Tag');
        h += '</div>';
        if (type === 'treeCondNode') {
            h += '<div class="ed-prop-section"><div class="ed-prop-head">条件</div>';
            h += prop('检查一次', 'checkbox', node, 'CheckOnce');
            h += prop('结果取反', 'checkbox', node, 'ReverseResult');
            h += '<div class="ed-prop-sub">条件列表: ' + ((node.TriggerConds||node.triggerConds||[]).length) + ' 项</div>';
            h += '</div>';
        }
        if (type === 'treeActionNode') {
            h += '<div class="ed-prop-section"><div class="ed-prop-head">动作</div>';
            h += '<div class="ed-prop-sub">动作列表: ' + ((node.TriggerActions||node.triggerActions||[]).length) + ' 项</div>';
            h += '</div>';
        }
        if (type === 'treeDelayNode') { h += '<div class="ed-prop-section"><div class="ed-prop-head">延迟</div>'; h += prop('秒数', 'number', node, 'Delay'); h += '</div>'; }
        if (type === 'treeLoop') { h += '<div class="ed-prop-section"><div class="ed-prop-head">循环</div>'; h += prop('次数', 'number', node, 'Times'); h += '</div>'; }
        if (type === 'treeScriptNode') { h += '<div class="ed-prop-section"><div class="ed-prop-head">脚本</div>'; h += '<textarea class="ed-prop-area" id="dfScript" style="width:100%;height:80px;font-size:11px;font-family:monospace">'+esc(node.Script||'')+'</textarea>'; h += '<button class="btn-sm" style="margin-top:4px" onclick="saveTreeScript()">保存脚本</button>'; h += '</div>'; }
        body.innerHTML = h;
        bindTreePropInputs();
        return;
    }

    hdr.textContent = '属性'; body.innerHTML = '<p class="props-hint">点击节点查看属性</p>';
}

function prop(label, type, obj, key) {
    var v = obj[key] !== undefined ? obj[key] : (type==='checkbox'?false:'');
    var id = 'p_'+Math.random().toString(36).substr(2,8);
    if (type === 'checkbox')
        return '<div class="ed-prop-row"><span class="ed-prop-label">'+label+'</span><input class="ed-prop-input" type="checkbox" id="'+id+'" data-key="'+key+'" '+(v?'checked':'')+'></div>';
    return '<div class="ed-prop-row"><span class="ed-prop-label">'+label+'</span><input class="ed-prop-input" type="'+type+'" id="'+id+'" data-key="'+key+'" value="'+esc(String(v))+'"></div>';
}

function bindTreePropInputs() {
    document.querySelectorAll('#edPropsBody .ed-prop-input').forEach(function(el) {
        if (el.dataset.bound) return; el.dataset.bound = '1';
        el.addEventListener('change', function() {
            var node = getNodeByPath(selectedNodePath);
            if (!node) return;
            var key = this.dataset.key;
            node[key] = this.type==='checkbox' ? this.checked : (this.type==='number' ? Number(this.value) : this.value);
            markDirty();
        });
    });
}

function saveTreeScript() {
    var node = getNodeByPath(selectedNodePath);
    var ta = document.getElementById('dfScript');
    if (node && ta) { node.Script = ta.value; markDirty(); setStatus('脚本已保存'); }
}

// ==================== 文件浏览器 ====================

async function pickDirectory() {
    if (!window.showDirectoryPicker) {
        setStatus('浏览器不支持目录选择','error'); return;
    }
    try {
        dirHandle = await window.showDirectoryPicker();
        document.getElementById('fileDir').textContent = dirHandle.name;
        refreshFileList();
    } catch(e) { if (e.name !== 'AbortError') setStatus('目录选择失败','error'); }
}

async function refreshFileList() {
    var list = document.getElementById('fileList');
    if (!dirHandle) { list.innerHTML = '<div class="props-hint">点"选择目录"浏览文件</div>'; return; }

    fileEntries = [];
    var ext = '.json';
    for await (var [name, handle] of dirHandle.entries()) {
        if (handle.kind === 'file' && name.endsWith(ext)) {
            fileEntries.push({ name: name, handle: handle });
        }
    }
    fileEntries.sort(function(a,b) { return a.name.localeCompare(b.name); });

    if (fileEntries.length === 0) {
        list.innerHTML = '<div class="props-hint">目录中无 .json 文件</div>';
        return;
    }

    var h = '';
    fileEntries.forEach(function(entry) {
        var cls = entry.name === currentFile ? ' active' : '';
        h += '<div class="file-item' + cls + '" data-file="' + esc(entry.name) + '">';
        h += '<span class="file-name">' + esc(entry.name) + '</span>';
        h += '</div>';
    });
    list.innerHTML = h;

    list.querySelectorAll('.file-item').forEach(function(item) {
        item.addEventListener('click', function() {
            if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
            openFromDir(this.dataset.file);
        });
    });
}

async function openFromDir(fileName) {
    var entry = fileEntries.find(function(e) { return e.name === fileName; });
    if (!entry) return;
    try {
        var file = await entry.handle.getFile();
        fileHandle = entry.handle;
        readFileObj(file);
    } catch(e) { setStatus('打开失败: ' + e.message, 'error'); }
}

// ==================== 文件操作 ====================

function newFile() {
    if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    timelineData = { Name:'新执行轴', TerritoryTypeId:0, Note:'', ExposedVars:[], TreeRoot:{ '$type':'AEAssist.Trigger.TriggerNode.TreeRoot, AEAssist', DisplayName:'Root', Id:1, Enable:true, Remark:'', Tag:'', Childs:[] } };
    currentFile=''; fileHandle=null; isDirty=true; selectedNodePath=null;
    switchAxis(); setStatus('已新建');
}
async function loadFile() {
    if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    if (window.showOpenFilePicker) {
        try { var handles = await window.showOpenFilePicker({ types:[{ description:'JSON', accept:{'application/json':['.json','.txt']} }] }); var file = await handles[0].getFile(); fileHandle = handles[0]; readFileObj(file); } catch(e) {}
    } else document.getElementById('edFileInput').click();
}
function readFileObj(file) {
    var r = new FileReader();
        r.onload = function() { try { timelineData = JSON.parse(r.result); } catch(ex) { setStatus('JSON解析失败: '+ex.message,'error'); return; } currentFile=file.name; isDirty=false; selectedNodePath=null; switchAxis(); setStatus('已加载: '+file.name,'success'); };
    r.readAsText(file);
}
async function saveFile() {
    if (!timelineData) { setStatus('无内容','error'); return; }
    var json = JSON.stringify(timelineData, null, 2);
    if (fileHandle && typeof fileHandle.createWritable === 'function') { try { var w = await fileHandle.createWritable(); await w.write(json); await w.close(); isDirty=false; updateFooter(); setStatus('已保存: '+currentFile,'success'); return; } catch(e) {} }
    saveFileAs();
}
async function saveFileAs() {
    if (!timelineData) { setStatus('无内容','error'); return; }
    var json = JSON.stringify(timelineData, null, 2); var name = currentFile || 'timeline.json';
    if (window.showSaveFilePicker) { try { var h = await window.showSaveFilePicker({ suggestedName:name, types:[{ description:'JSON', accept:{'application/json':['.json']} }] }); var w = await h.createWritable(); await w.write(json); await w.close(); fileHandle=h; currentFile=h.name; isDirty=false; updateFooter(); setStatus('已保存: '+currentFile,'success'); return; } catch(e) { if(e.name==='AbortError')return; } }
    downloadJson(json, name);
}
function exportFile() { if (!timelineData) { setStatus('无内容','error'); return; } downloadJson(JSON.stringify(timelineData,null,2), currentFile||'export.json'); setStatus('已导出'); }
function downloadJson(json, name) { var b=new Blob([json],{type:'application/json'}); var u=URL.createObjectURL(b); var a=document.createElement('a'); a.href=u; a.download=name; a.click(); URL.revokeObjectURL(u); }
// ==================== 工具 ====================

function markDirty() { isDirty = true; renderTree(); renderProps(); updateFooter(); }
function updateFooter() {
    var labels = { execution:'执行轴', assist:'辅助轴' };
    var l = labels[currentAxis]||'';
    document.getElementById('edFooter').textContent = currentFile ? l + ' — ' + currentFile + (isDirty?' (未保存)':'') : '就绪' + (isDirty?' (未保存)':'');
    // 更新信息面板
    var el = document.getElementById('infoType');
    if (el) el.textContent = l;
    el = document.getElementById('infoFile');
    if (el) el.textContent = currentFile || '—';
    el = document.getElementById('infoCount');
    if (el) el.textContent = countNodes();
    el = document.getElementById('infoDirty');
    if (el) { el.textContent = isDirty ? '已修改' : '已保存'; el.style.color = isDirty ? 'var(--orange)' : 'var(--green)'; }
}

function countNodes() {
    if (!timelineData) return 0;
    var c = 0;
    function w(node) { if(!node) return; c++; (node.Childs||node.childs||[]).forEach(w); }
    w(timelineData.TreeRoot||timelineData.treeRoot);
    return c;
}
function setStatus(msg, type) {
    var el = document.getElementById('edStatus'); el.textContent = msg; el.className = 'status '+(type||'');
    if (type==='success') setTimeout(function(){ if(el.textContent===msg){ el.textContent=''; el.className='status'; } }, 3000);
}

function iosSelect(opts, selVal, onchange) {
    var h = '<div class="ios-sel" onclick="event.stopPropagation();this.classList.toggle(\'open\')">';
    opts.forEach(function(opt) {
        if (typeof opt === 'string') opt = {value:opt, label:opt};
        var isSel = opt.value===selVal;
        // 当前选中的选项不阻止冒泡，让父元素处理点击展开/收起
        var clickHandler = isSel ? '' : ' onclick="event.stopPropagation();'+onchange+';this.parentElement.classList.remove(\'open\')"';
        h += '<div class="ios-sel-opt'+(isSel?' sel':'')+'" data-val="'+esc(opt.value)+'"'+clickHandler+'>'+esc(opt.label)+'</div>';
    });
    return h+'</div>';
}
function iosSelectVal(sel, val) { sel.value = val; sel.querySelectorAll('.ios-sel-opt').forEach(function(o){o.classList.toggle('sel',o.dataset.val===val)}); }

function esc(s) { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
