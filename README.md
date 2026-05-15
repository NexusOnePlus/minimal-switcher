# Minimal Switcher - Liquid Glass Effect

A minimal Alt-Tab replacement for Windows with a real-time **Liquid Glass** shader effect that distorts the background in real-time.

## Features
- 🎨 **Real-time liquid glass shader** - Distorts the desktop, videos, and apps behind the window using a pixel shader
- ⚡ **Per-frame capture** - Uses `CompositionTarget.Rendering` for ~60fps updates
- 🖥️ **Multi-monitor aware** - Centers on the current monitor and captures the correct display
- 🎥 **Windows Graphics Capture API** - Modern screen capture that excludes our window automatically
- 🎯 **Minimal UI** - Shows only the selected app icon with title, no distractions
- 🔵 **Rounded corners** - Smooth, modern appearance with configurable radius
- 🌫️ **Adjustable blur** - Configurable `BlurIntensity` parameter for frosted glass effect

## Architecture

### Shader System
The liquid glass effect is implemented as a **3-layer visual tree**:

```
┌─────────────────────────────────────────┐
│ Window (AllowsTransparency=True)        │
├─────────────────────────────────────────┤
│ Layer 3: ContentOverlay (#2A000000)    │
│           - Dark overlay for readability │
├─────────────────────────────────────────┤
│ Layer 2: GlassyLayer (GlassyEffect)    │
│           - Pixel shader distorts input │
│           - Input = desktop capture     │
├─────────────────────────────────────────┤
│ Layer 1: WindowFrame (Desktop capture)  │
│           - ImageBrush with screenshot   │
│           - Cropped to window bounds    │
└─────────────────────────────────────────┘
```

- **Shader**: `Shaders/GlassyEffect.ps` (compiled pixel shader binary)
- **Wrapper**: `GlassyEffect.cs` (WPF `ShaderEffect` subclass)
- **Parameters**: `TextureSize`, `GlassCenter`, `GlassSize`, `BlurIntensity` (default: 0.6 for frosted effect)

### Capture Pipeline (Windows Graphics Capture)
1. **`CompositionTarget.Rendering`** triggers per-frame
2. **`ScreenCaptureHelper.CaptureFullScreen()`** captures the monitor using **Windows Graphics Capture API**
3. **Automatic exclusion** - Our window is excluded from capture automatically (no flicker)
4. **`UpdateBackdrop()`** crops the snapshot to the window bounds using `ImageBrush.Viewbox`
5. **Shader** distorts the captured desktop in real-time with adjustable blur

### Key Fixes Applied
- ✅ **Fixed self-capture**: Uses Windows Graphics Capture API which excludes our window automatically
- ✅ **No flicker**: Removed window hide/show during capture (Graphics Capture handles this natively)
- ✅ **Real-time**: Uses `CompositionTarget.Rendering` for ~60fps updates
- ✅ **Proper layering**: `WindowFrame.Background` holds desktop capture, `GlassyLayer` applies shader
- ✅ **Multi-monitor centering**: Window centers on current monitor (DPI-aware)
- ✅ **Recorder-friendly**: Other screen recorders can capture our window normally

## Configuration

### Adjusting Blur Intensity
In `MainWindow.xaml.cs`, modify the `BlurIntensity` parameter:
```csharp
_glassyEffect.BlurIntensity = 0.6f; // Range: 0.0 (no blur) to 2.0+ (heavy distortion)
```

### Changing Window Roundness
In `MainWindow.xaml`, modify the `CornerRadius` on all borders:
```xaml
<Border x:Name="WindowFrame" CornerRadius="20"> <!-- Default: 14 -->
```

## Usage
1. Build and run `minimal-switcher.exe`
2. Press **Alt+Tab** to trigger the switcher
3. Keep pressing **Tab** to cycle through apps
4. Release **Alt** to switch to the selected app

## Requirements
- Windows 10 (1903+) or Windows 11
- .NET 10 with WPF support
- Windows App SDK / WinRT support for Graphics Capture

## Notes
- The shader binary (`GlassyEffect.ps`) was compiled externally (HLSL source not included)
- Other screen recorders can capture this window normally (Graphics Capture only excludes during our capture)
- For performance, consider reducing capture resolution or frame rate if needed
