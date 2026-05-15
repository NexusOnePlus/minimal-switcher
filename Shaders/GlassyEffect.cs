using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace minimal_switcher.Shaders;

public sealed class GlassyEffect : ShaderEffect
{
    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(nameof(Input), typeof(GlassyEffect), 0);

    public static readonly DependencyProperty TextureSizeProperty =
        DependencyProperty.Register(
            nameof(TextureSize),
            typeof(global::System.Windows.Point),
            typeof(GlassyEffect),
            new UIPropertyMetadata(new global::System.Windows.Point(1.0, 1.0), PixelShaderConstantCallback(0)));

    public static readonly DependencyProperty GlassCenterProperty =
        DependencyProperty.Register(
            nameof(GlassCenter),
            typeof(global::System.Windows.Point),
            typeof(GlassyEffect),
            new UIPropertyMetadata(new global::System.Windows.Point(0.0, 0.0), PixelShaderConstantCallback(1)));

    public static readonly DependencyProperty GlassSizeProperty =
        DependencyProperty.Register(
            nameof(GlassSize),
            typeof(global::System.Windows.Point),
            typeof(GlassyEffect),
            new UIPropertyMetadata(new global::System.Windows.Point(120.0, 80.0), PixelShaderConstantCallback(2)));

    public static readonly DependencyProperty BlurIntensityProperty =
        DependencyProperty.Register(
            nameof(BlurIntensity),
            typeof(float),
            typeof(GlassyEffect),
            new UIPropertyMetadata(1.2f, PixelShaderConstantCallback(3)));

    public GlassyEffect()
    {
        PixelShader = new PixelShader
        {
            UriSource = new Uri("pack://application:,,,/minimal-switcher;component/Shaders/GlassyEffect.ps", UriKind.Absolute)
        };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(TextureSizeProperty);
        UpdateShaderValue(GlassCenterProperty);
        UpdateShaderValue(GlassSizeProperty);
        UpdateShaderValue(BlurIntensityProperty);
    }

    public global::System.Windows.Media.Brush Input
    {
        get => (global::System.Windows.Media.Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public global::System.Windows.Point TextureSize
    {
        get => (global::System.Windows.Point)GetValue(TextureSizeProperty);
        set => SetValue(TextureSizeProperty, value);
    }

    public global::System.Windows.Point GlassCenter
    {
        get => (global::System.Windows.Point)GetValue(GlassCenterProperty);
        set => SetValue(GlassCenterProperty, value);
    }

    public global::System.Windows.Point GlassSize
    {
        get => (global::System.Windows.Point)GetValue(GlassSizeProperty);
        set => SetValue(GlassSizeProperty, value);
    }

    public float BlurIntensity
    {
        get => (float)GetValue(BlurIntensityProperty);
        set => SetValue(BlurIntensityProperty, value);
    }
}
