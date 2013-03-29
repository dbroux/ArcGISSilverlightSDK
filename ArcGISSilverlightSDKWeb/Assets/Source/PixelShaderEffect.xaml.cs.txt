using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcGISSilverlightSDK
{
    public partial class PixelShaderEffect : UserControl
    {
        public PixelShaderEffect()
        {
            InitializeComponent();
            MyMap.Effect = new MonochromeEffect();
        }
    }

    public class MonochromeEffect : ShaderEffect
    {
        /// <summary>
        /// Gets or sets the FilterColor variable within the shader.
        /// </summary>
        public static readonly DependencyProperty FilterColorProperty = DependencyProperty.Register("FilterColor", typeof(Color), typeof(MonochromeEffect), new PropertyMetadata(Colors.White, PixelShaderConstantCallback(0)));

        /// <summary>
        /// Gets or sets the Input of the shader.
        /// </summary>
        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(MonochromeEffect), 0);

        /// <summary>
        /// Creates an instance and updates the shader's variables to the default values.
        /// </summary>
        public MonochromeEffect()
        {
            PixelShader = new PixelShader
            {
                UriSource = new Uri("/ArcGISSilverlightSDK;component/Graphics/Monochrome.ps", UriKind.RelativeOrAbsolute)
            };

            UpdateShaderValue(FilterColorProperty);
            UpdateShaderValue(InputProperty);
        }

        /// <summary>
        /// Gets or sets the FilterColor variable within the shader.
        /// </summary>
        public Color FilterColor
        {
            get { return (Color)GetValue(FilterColorProperty); }
            set { SetValue(FilterColorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the input used in the shader.
        /// </summary>
        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }
    }

}
