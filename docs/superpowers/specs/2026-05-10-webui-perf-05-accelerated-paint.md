# WebUI 性能优化 — 方向 5: OnAcceleratedPaint GPU 直通渲染

## 问题

当前 CEF 渲染走 `OnPaint` 路径，每帧经历：

```
CEF 渲染到 CPU 侧 BGRA 缓冲区
  → Buffer.MemoryCopy → _alphaLookupBuffer（全帧 CPU→CPU 拷贝）
  → UpdateSubresource → _viewTexture（CPU→GPU PCIe 传输）
  → CopySubresourceRegion → _sharedTexture（GPU→GPU 合成）
  → Flush()（阻塞 GPU 流水线）
  → GetSharedHandle → IPC → 插件侧 OpenSharedResource
```

`OnAcceleratedPaint` 是 CEF 提供的 GPU 直通 API（CefSharp 自 v91+ 支持），但原 Browsingway 实现中直接抛了 `NotImplementedException`。

## 约束

- 结合方向 1（内容自适应），窗口尺寸精确匹配控制按钮区域，**不需要 alpha 点击穿透检测**
- 插件侧 IPC 和纹理渲染接口不变（`SharedTextureHandler`、`UpdateTexture` 消息）
- CefSharp v143 原生支持 `OnAcceleratedPaint`

## 设计

### 管线对比

```
OnPaint（当前）                              OnAcceleratedPaint（改造后）

CEF OffScreen CPU 渲染                       CEF GPU 渲染
  ↓ BGRA 缓冲区（CPU 内存）                       ↓ GPU 纹理（CEF 内部管理）
  ↓ Buffer.MemoryCopy（CPU→CPU，全帧）              ↓ OpenSharedResource（handle 直达）
  ↓ UpdateSubresource（CPU→GPU PCIe）               ↓ CopySubresourceRegion（GPU→GPU）
  ↓ CopySubresourceRegion（GPU→GPU 合成）            ↓ CopySubresourceRegion（弹层合成）
  ↓ Flush()（阻塞 GPU 流水线）                      ↓ IPC UpdateTexture
  ↓ GetSharedHandle（内核态）
  ↓ IPC UpdateTexture
  → 插件侧：完全不变                              → 插件侧：完全不变
```

### CEF 配置（CefHandler.cs）

```csharp
// 移除（与 GPU accelerated paint 不兼容）
// settings.SetOffScreenRenderingBestPerformanceArgs();
// 因为该方法设置了 --disable-surfaces = 1，禁用了 GPU 合成

// 新增
settings.EnableAcceleratedPaint = true;
```

### TextureRenderHandler — OnAcceleratedPaint 实现

```csharp
private ID3D11Texture2D* _viewTexture;   // 视图层纹理
private ID3D11Texture2D* _popupTexture;  // 弹层纹理
private ID3D11Texture2D* _sharedTexture;  // 合成后发给插件的共享纹理
private bool _popupVisible;
private Point _popupPos;

public void OnAcceleratedPaint(PaintElementType type, Rect dirtyRect, AcceleratedPaintInfo info)
{
    ID3D11DeviceContext* context;
    DxHandler.Device->GetImmediateContext(&context);

    // 打开 CEF 的 GPU 纹理（handle → texture，无数据拷贝）
    ID3D11Texture2D* cefTexture;
    device->OpenSharedResource(info.SharedTextureHandle, &cefTexture);

    // 根据类型拷贝到对应的内部纹理
    ID3D11Texture2D* target = type == PaintElementType.View ? _viewTexture : _popupTexture;
    context->CopySubresourceRegion(target, 0, 0, 0, 0, cefTexture, 0, null);

    cefTexture->Release(); // CEF 的纹理引用已用完

    // 合成到 _sharedTexture
    context->CopySubresourceRegion(_sharedTexture, 0, 0, 0, 0, _viewTexture, 0, null);
    if (_popupVisible && _popupTexture != null)
    {
        context->CopySubresourceRegion(
            _sharedTexture, 0,
            (uint)_popupPos.X, (uint)_popupPos.Y, 0,
            _popupTexture, 0, null);
    }

    context->Release();
    // 不需要 Flush() — GPU→GPU 拷贝由驱动管线管理
}
```

### 删除内容

| 删除项 | 原因 |
|--------|------|
| `OnPaint()` 方法体 | 由 `OnAcceleratedPaint` 完全替代 |
| `_alphaLookupBuffer` | 不需要 alpha 穿透检测 |
| `_alphaLookupBufferWidth/Height` | 同上 |
| `GetAlphaAt()` | 同上 |
| `SetMousePosition()` RPC | 同上 |
| `_cursorOnBackground` | 同上 |
| `_renderLock` | 无 CPU 缓冲区争用，GPU 拷贝由驱动串行化 |
| `context->Flush()` | GPU→GPU 拷贝不需要立即提交 |
| `Buffer.MemoryCopy` | 全帧 CPU 拷贝不再需要 |
| `_obsoleteTextures` 延迟释放 | Resize 时可直接释放旧纹理 |

### 保留不变

- `_viewTexture` / `_popupTexture` / `_sharedTexture` 纹理创建逻辑
- `SharedTextureHandle` 属性（共享 handle 懒初始化）
- `Resize()` 方法（仅重建纹理尺寸）
- `OnPopupShow()` / `OnPopupSize()` 弹层逻辑
- 所有 IPC 消息（`UpdateTexture` handle 不变）
- 插件侧 `SharedTextureHandler` / `OpenSharedResource` / `ImGui.Image`

### 修改清单

| 文件 | 改动 |
|------|------|
| `Browsingway.Renderer/CefHandler.cs` | 启用 `EnableAcceleratedPaint`，移除 `SetOffScreenRenderingBestPerformanceArgs` |
| `Browsingway.Renderer/TextureRenderHandler.cs` | 实现 `OnAcceleratedPaint`，删除 OnPaint 全部逻辑，删除 alpha 检测，移除 `_renderLock`/`_obsoleteTextures`/`Flush` |
| `Browsingway/Overlay.cs` | 移除 `SetCursor` 中对 `BrowsingwayNoCapture` 的处理（可选简化） |
| 插件侧其他文件 | **无变化** |

### 风险与注意事项

- **`--disable-surfaces` 移除**：该参数强制 CEF 不使用 GPU 合成表面，与 `OnAcceleratedPaint` 不兼容。移除后 CEF 将使用 GPU 合成，可能增加显存占用。监控显存使用量
- **CEF 版本兼容**：CefSharp v143 已稳定支持。如有降级需求，`EnableAcceleratedPaint` / `AcceleratedPaintInfo` API 需要检查
- **弹层合成**：当前 `OnPaint` 中用 `CopySubresourceRegion` 手动合成 view + popup，`OnAcceleratedPaint` 中沿用同样方式。CEF 也支持自动合成但需要用不同的 CEF 配置——此处保持手动合成与原有行为一致
