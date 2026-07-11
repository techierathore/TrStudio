# TrStudioUI/wwwroot — host asset wiring

TrStudioUI itself ships no static assets; TrBlazeUI's prebuilt CSS comes from its NuGet
package. Every HOST app (TrStudio, TrStudioAdmin) must link these stylesheets in
`Components/App.razor`, **in this order**:

```html
<link rel="stylesheet" href="styles/theme.css" />                            <!-- host-owned CSS variables (OKLCH) -->
<link rel="stylesheet" href="_content/TrBlazeUI.Components/trblazeui.css" /> <!-- prebuilt TrBlazeUI styles -->
<link rel="stylesheet" href="styles/base.css" />                             <!-- host base styles -->
```

Hosts must also register services in `Program.cs`:

```csharp
builder.Services.AddTrBlazeUIPrimitives();   // TrBlazeUI.Primitives.Extensions
builder.Services.AddScoped<ToastService>();  // TrBlazeUI.Components.Toast
```

and route with this RCL's assembly:

```razor
<Router AppAssembly="typeof(Program).Assembly"
        AdditionalAssemblies="new[] { typeof(TrStudioUI.Layout.MainLayout).Assembly }">
```

Dark mode = add `class="dark"` on `<html>`. Theme variables follow shadcn/tweakcn conventions
(see `.trblazeui/TrBlazeUI-AI-Reference.md` §2).
