## EgbimotoApp.cs + RibbonBuilder.cs — v11 MCP ribbon state hook

### 1. RibbonBuilder.cs — MCP butonu AvailabilityClassName

BuildStaticPanels() içindeki MCP butonuna şunu ekle:

```csharp
// ÖNCE:
AddPush(pOto, asm, "EG_MCP", "MCP\nServer",
    "EGBIMOTO.Addin.Commands.McpServerToggleCommand",
    "EGBIMOTO MCP Server'ı başlatır/durdurur (port 5577).",
    iconDir, "mcp.png");

// SONRA — AddPush içinde PushButtonData oluşturulduktan sonra
// AvailabilityClassName set et:
// (AddPush metodunu genişlet veya aşağıdaki inline kullanımı yap)

var mcpData = new PushButtonData("EG_MCP", "MCP\nServer", asm,
    "EGBIMOTO.Addin.Commands.McpServerToggleCommand")
{
    ToolTip               = "EGBIMOTO MCP Server'ı başlatır/durdurur (port 5577).",
    // v11: AvailabilityClassName — ribbon refresh için
    AvailabilityClassName = "EGBIMOTO.Addin.Commands.McpServerAvailability",
};
var mcpImg = LoadIcon(iconDir, "mcp.png");
if (mcpImg != null) mcpData.LargeImage = mcpImg;
try { pOto.AddItem(mcpData); } catch { }
```

### 2. EgbimotoApp.cs — OnIdling ile ToolTip refresh

IExternalApplication.OnStartup() içinde (Bootstrap / Application sınıfı):

```csharp
// v11: MCP ribbon tooltip'ini server durumuna göre güncelle
app.Idling += (sender, e) =>
{
    if (sender is UIApplication uiApp)
        McpServerAvailability.RefreshMcpButtonTooltip(uiApp);
};
```

OnIdling her Revit idle anında (yakl. her 500ms) çağrılır.
RefreshMcpButtonTooltip() GetRibbonPanels() + GetItems() geziyor,
try/catch ile hata sessizce yutulur — Revit startup sırasında
ribbon henüz hazır olmayabilir.
