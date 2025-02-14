﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Xamarin.Forms.PlatformConfiguration.WindowsSpecific;
using Specifics = Xamarin.Forms.PlatformConfiguration.WindowsSpecific.Label;

#if __IOS__ || __ANDROID__
using NativeTextBlok = Windows.UI.Xaml.Controls.Border;
#else
using NativeTextBlok = Windows.UI.Xaml.Controls.TextBlock;
#endif

namespace Xamarin.Forms.Platform.UWP
{
	public static partial class FormattedStringExtensions
	{
		public static Run ToRun(this Span span)
		{
			var run = new Run { Text = span.Text ?? string.Empty };

			if (span.TextColor != Color.Default)
				run.Foreground = span.TextColor.ToBrush();

			if (!span.IsDefault())
#pragma warning disable 618
				run.ApplyFont(span.Font);
#pragma warning restore 618

			return run;
		}
	}

	public partial class LabelRenderer : ViewRenderer<Label, NativeTextBlok>
	{
		bool _fontApplied;
		bool _isInitiallyDefault;
		SizeRequest _perfectSize;
		bool _perfectSizeValid;
		IList<double> _inlineHeights = new List<double>();

		protected override AutomationPeer OnCreateAutomationPeer()
		{
			// We need an automation peer so we can interact with this in automated tests
			if (Control == null)
			{
				return new FrameworkElementAutomationPeer(this);
			}

			return new FrameworkElementAutomationPeer(Control);
		}

		private Windows.UI.Xaml.Controls.TextBlock TextBlockControl
#if __IOS__ || __ANDROID__
			=> Control.Child as Windows.UI.Xaml.Controls.TextBlock;
#else
			=> Control;
#endif

		protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
		{
			if (Element == null)
				return finalSize;

			double childHeight = Math.Max(0, Math.Min(Element.Height, Control.DesiredSize.Height));
			var rect = new Rect();

			switch (Element.VerticalTextAlignment)
			{
				case Xamarin.Forms.TextAlignment.Start:
					break;
				default:
				case Xamarin.Forms.TextAlignment.Center:
					rect.Y = (int)((finalSize.Height - childHeight) / 2);
					break;
				case Xamarin.Forms.TextAlignment.End:
					rect.Y = finalSize.Height - childHeight;
					break;
			}
			rect.Height = childHeight;
			rect.Width = finalSize.Width;
			Control.Arrange(rect);
			TextBlockControl.RecalculateSpanPositions(Element, _inlineHeights);
			return finalSize;
		}

		public override SizeRequest GetDesiredSize(double widthConstraint, double heightConstraint)
		{
			if (!_perfectSizeValid)
			{
				_perfectSize = base.GetDesiredSize(double.PositiveInfinity, double.PositiveInfinity);
				_perfectSize.Minimum = new Size(Math.Min(10, _perfectSize.Request.Width), _perfectSize.Request.Height);
				_perfectSizeValid = true;
			}

			var widthFits = widthConstraint >= _perfectSize.Request.Width;
			var heightFits = heightConstraint >= _perfectSize.Request.Height;

			if (widthFits && heightFits)
				return _perfectSize;

			var result = base.GetDesiredSize(widthConstraint, heightConstraint);
			var tinyWidth = Math.Min(10, result.Request.Width);
			result.Minimum = new Size(tinyWidth, result.Request.Height);

			if (widthFits || Element.LineBreakMode == LineBreakMode.NoWrap)
				return result;

			bool containerIsNotInfinitelyWide = !double.IsInfinity(widthConstraint);

			if (containerIsNotInfinitelyWide)
			{
				bool textCouldHaveWrapped = Element.LineBreakMode == LineBreakMode.WordWrap || Element.LineBreakMode == LineBreakMode.CharacterWrap;
				bool textExceedsContainer = result.Request.Width > widthConstraint;

				if (textExceedsContainer || textCouldHaveWrapped)
				{
					var expandedWidth = Math.Max(tinyWidth, widthConstraint);
					result.Request = new Size(expandedWidth, result.Request.Height);
				}
			}

			return result;
		}

		protected override void OnElementChanged(ElementChangedEventArgs<Label> e)
		{
			base.OnElementChanged(e);

			if (e.NewElement != null)
			{
				if (Control == null)
				{
#if __IOS__ || __ANDROID__
					SetNativeControl(new Border { Child = new TextBlock() });
#else
					SetNativeControl(new TextBlock());
#endif
				}

				_isInitiallyDefault = Element.IsDefault();

				UpdateText(TextBlockControl);
				UpdateColor(TextBlockControl);
				UpdateAlign(TextBlockControl);
				UpdateFont(TextBlockControl);
				UpdateLineBreakMode(TextBlockControl);
				UpdateDetectReadingOrderFromContent(TextBlockControl);
			}
		}

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == Label.TextProperty.PropertyName || e.PropertyName == Label.FormattedTextProperty.PropertyName)
				UpdateText(TextBlockControl);
			else if (e.PropertyName == Label.TextColorProperty.PropertyName)
				UpdateColor(TextBlockControl);
			else if (e.PropertyName == Label.HorizontalTextAlignmentProperty.PropertyName || e.PropertyName == Label.VerticalTextAlignmentProperty.PropertyName)
				UpdateAlign(TextBlockControl);
			else if (e.PropertyName == Label.FontProperty.PropertyName)
				UpdateFont(TextBlockControl);
			else if (e.PropertyName == Label.LineBreakModeProperty.PropertyName)
				UpdateLineBreakMode(TextBlockControl);
			else if (e.PropertyName == VisualElement.FlowDirectionProperty.PropertyName)
				UpdateAlign(TextBlockControl);
			else if (e.PropertyName == Specifics.DetectReadingOrderFromContentProperty.PropertyName)
				UpdateDetectReadingOrderFromContent(TextBlockControl);
			else if (e.PropertyName == Label.LineHeightProperty.PropertyName)
				UpdateLineHeight(TextBlockControl);
			base.OnElementPropertyChanged(sender, e);
		}

		void UpdateAlign(TextBlock textBlock)
		{
			_perfectSizeValid = false;

			if (textBlock == null)
				return;

			Label label = Element;
			if (label == null)
				return;

			textBlock.TextAlignment = label.HorizontalTextAlignment.ToNativeTextAlignment(((IVisualElementController)Element).EffectiveFlowDirection);
			textBlock.VerticalAlignment = label.VerticalTextAlignment.ToNativeVerticalAlignment();
		}

		void UpdateColor(TextBlock textBlock)
		{
			if (textBlock == null)
				return;

			Label label = Element;
			if (label != null && label.TextColor != Color.Default)
			{
				textBlock.Foreground = label.TextColor.ToBrush();
			}
			else
			{
				textBlock.ClearValue(TextBlock.ForegroundProperty);
			}
		}

		void UpdateFont(TextBlock textBlock)
		{
			_perfectSizeValid = false;

			if (textBlock == null)
				return;

			Label label = Element;
			if (label == null || (label.IsDefault() && !_fontApplied))
				return;

#pragma warning disable 618
			Font fontToApply = label.IsDefault() && _isInitiallyDefault ? Font.SystemFontOfSize(NamedSize.Medium) : label.Font;
#pragma warning restore 618

			textBlock.ApplyFont(fontToApply);
			_fontApplied = true;
		}

		void UpdateLineBreakMode(TextBlock textBlock)
		{
			_perfectSizeValid = false;

			if (textBlock == null)
				return;

			switch (Element.LineBreakMode)
			{
				case LineBreakMode.NoWrap:
					textBlock.TextTrimming = TextTrimming.Clip;
					textBlock.TextWrapping = TextWrapping.NoWrap;
					break;
				case LineBreakMode.WordWrap:
					textBlock.TextTrimming = TextTrimming.None;
					textBlock.TextWrapping = TextWrapping.Wrap;
					break;
				case LineBreakMode.CharacterWrap:
					textBlock.TextTrimming = TextTrimming.WordEllipsis;
					textBlock.TextWrapping = TextWrapping.Wrap;
					break;
				case LineBreakMode.HeadTruncation:
					// TODO: This truncates at the end.
					textBlock.TextTrimming = TextTrimming.WordEllipsis;
					textBlock.TextWrapping = TextWrapping.NoWrap;
					break;
				case LineBreakMode.TailTruncation:
					textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
					textBlock.TextWrapping = TextWrapping.NoWrap;
					break;
				case LineBreakMode.MiddleTruncation:
					// TODO: This truncates at the end.
					textBlock.TextTrimming = TextTrimming.WordEllipsis;
					textBlock.TextWrapping = TextWrapping.NoWrap;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		void UpdateText(TextBlock textBlock)
		{
			_perfectSizeValid = false;

			if (textBlock == null)
				return;

			Label label = Element;
			if (label != null)
			{
				FormattedString formatted = label.FormattedText;

				if (formatted == null)
				{
					textBlock.Text = label.Text ?? string.Empty;
				}
				else
				{
					textBlock.Inlines.Clear();
					// Have to implement a measure here, otherwise inline.ContentStart and ContentEnd will be null, when used in RecalculatePositions
					textBlock.Measure(new Windows.Foundation.Size(double.MaxValue, double.MaxValue));

					var heights = new List<double>();
					for (var i = 0; i < formatted.Spans.Count; i++)
					{
						var span = formatted.Spans[i];

						var run = span.ToRun();
						heights.Add(TextBlockControl.FindDefaultLineHeight(run));
						textBlock.Inlines.Add(run);
					}
					_inlineHeights = heights;
				}
			}
		}

		void UpdateDetectReadingOrderFromContent(TextBlock textBlock)
		{
			if (Element.IsSet(Specifics.DetectReadingOrderFromContentProperty))
			{
				if (Element.OnThisPlatform().GetDetectReadingOrderFromContent())
				{
					textBlock.TextReadingOrder = TextReadingOrder.DetectFromContent;
				}
				else
				{
					textBlock.TextReadingOrder = TextReadingOrder.UseFlowDirection;
				}
			}
		}

		void UpdateLineHeight(TextBlock textBlock) 
		{
			if (textBlock == null)
				return;
			
			if (Element.LineHeight >= 0)
			{
				textBlock.LineHeight = Element.LineHeight * textBlock.FontSize;
			}
		}
	}
}
