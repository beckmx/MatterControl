﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public interface ITab
	{
		GuiWidget TabContent { get; }
		string Key { get; }
		string Text { get; }
	}

	/// <summary>
	/// A toolbar and associated tab body
	/// </summary>
	public class SimpleTabs : FlowLayoutWidget
	{
		public SimpleTabs(ThemeConfig theme, GuiWidget rightAnchorItem = null)
			: base(FlowDirection.TopToBottom)
		{
			this.TabContainer = this;

			if (rightAnchorItem == null)
			{
				TabBar = new OverflowBar(theme)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};
			}
			else
			{
				TabBar = new Toolbar(theme, rightAnchorItem)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};
			}

			this.AddChild(this.TabBar);
		}

		public Toolbar TabBar { get; }

		public GuiWidget TabContainer { get; protected set; }

		public event EventHandler ActiveTabChanged;

		private List<ITab> _allTabs = new List<ITab>();
		public IEnumerable<ITab> AllTabs => _allTabs;

		public int TabCount => _allTabs.Count;

		public virtual void AddTab(GuiWidget tabWidget, int position = -1)
		{
			var iTab = tabWidget as ITab;

			if (position == -1)
			{
				_allTabs.Add(iTab);
			}
			else
			{
				_allTabs.Insert(position - 1, iTab);
			}

			tabWidget.Click += TabWidget_Click;

			this.TabBar.ActionArea.AddChild(tabWidget, position);

			this.TabContainer.AddChild(iTab.TabContent);
		}

		public virtual void AddTab(GuiWidget tabWidget, int tabPosition, int widgetPosition)
		{
			var iTab = tabWidget as ITab;
			_allTabs.Insert(tabPosition, iTab);

			tabWidget.Click += TabWidget_Click;

			this.TabBar.ActionArea.AddChild(tabWidget, widgetPosition);

			this.TabContainer.AddChild(iTab.TabContent);
		}

		private void TabWidget_Click(object sender, MouseEventArgs e)
		{
			var tab = sender as ITab;
			this.ActiveTab = tab;

			// Push focus to tab content on tab pill selection
			tab.TabContent.Focus();
		}

		public string SelectedTabKey
		{
			get => this.ActiveTab.Key;
			set
			{
				var foundTab = _allTabs[0];
				foreach (var tab in _allTabs)
				{
					if (tab.Key == value)
					{
						foundTab = tab;
					}
				}

				this.ActiveTab = foundTab;
			}
		}

		public int SelectedTabIndex
		{
			get => _allTabs.IndexOf(this.ActiveTab);
			set
			{
				this.ActiveTab = _allTabs[value];
			}
		}

		internal virtual void RemoveTab(ITab tab)
		{
			_allTabs.Remove(tab);

			// Close Tab and TabContent widgets
			tab.TabContent.Close();
			(tab as GuiWidget)?.Close();

			if (tab is ChromeTab chromeTab)
			{
				// Activate next or last tab
				ActiveTab = chromeTab.NextTab ?? _allTabs.LastOrDefault();
			}
			else
			{
				// Activate last tab
				ActiveTab = _allTabs.LastOrDefault();
			}
		}

		private ITab _activeTab;
		public ITab ActiveTab
		{
			get => _activeTab;
			set
			{
				if (_activeTab != value)
				{
					_activeTab = value;

					var clickedWidget = value as GuiWidget;

					foreach (var tab in _allTabs)
					{
						tab.TabContent.Visible = (tab == clickedWidget);
					}

					this.OnActiveTabChanged();
				}
			}
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (this.TabContainer == this)
			{
				base.AddChild(childToAdd, indexInChildrenList);
			}
			else
			{
				this.TabContainer.AddChild(childToAdd, indexInChildrenList);
			}
		}

		protected virtual void OnActiveTabChanged()
		{
			this.ActiveTabChanged?.Invoke(this, null);
		}

		internal int GetTabIndex(SimpleTab tab)
		{
			return _allTabs.IndexOf(tab);
		}
	}

	public class ChromeTabs : SimpleTabs
	{
		private TabTrailer tabTrailer;

		private GuiWidget leadingTabAdornment;

		public event EventHandler PlusClicked;

		public ChromeTabs(GuiWidget rightAnchorItem, ThemeConfig theme)
			: base(theme, rightAnchorItem)
		{
			leadingTabAdornment = new GuiWidget()
			{
				MinimumSize = new Vector2(16, theme.TabButtonHeight),
				VAnchor = VAnchor.Bottom
			};
			leadingTabAdornment.AfterDraw += (s, e) =>
			{
				var firstItem = this.AllTabs.OfType<ChromeTab>().FirstOrDefault();
				ChromeTab.DrawTabLowerRight(e.Graphics2D, leadingTabAdornment.LocalBounds, (firstItem == this.ActiveTab) ? theme.BackgroundColor : theme.InactiveTabColor);
			};
			this.TabBar.ActionArea.AddChild(leadingTabAdornment);

			tabTrailer = new TabTrailer(this, theme)
			{
				VAnchor = VAnchor.Bottom,
				MinimumSize = new Vector2(8, theme.TabButtonHeight),
			};

			this.TabBar.ActionArea.AddChild(tabTrailer);

			var plusTabButton = new NewTabButton(AggContext.StaticData.LoadIcon("fa-plus_12.png", theme.InvertIcons), theme)
			{
				Height = 20,
			};

			plusTabButton.IconButton.Click += (s, e) =>
			{
				this.PlusClicked?.Invoke(this, null);
			};

			this.TabBar.ActionArea.AddChild(plusTabButton);
		}

		public override void AddTab(GuiWidget tabWidget, int tabIndex = -1)
		{
			// Default position if tabIndex == -1 is just before the tabTrailer
			var widgetPosition = this.TabBar.ActionArea.Children.IndexOf(tabTrailer);
			var firstTabPosition = this.TabBar.ActionArea.Children.IndexOf(leadingTabAdornment) + 1;

			if (tabIndex != -1)
			{
				// Adjust position to be the head of the list + the tabIndex offset
				widgetPosition = firstTabPosition + tabIndex;
			}

			if (tabWidget is ChromeTab newTab)
			{
				newTab.TabContent.Visible = false;

				// Call AddTab(widget, int) in base explicitly
				base.AddTab(tabWidget, widgetPosition - firstTabPosition, widgetPosition);

				tabTrailer.LastTab = this.AllTabs.LastOrDefault();

				// Listen for tab changes and update links
				/*
				newTab.VisibleChanged += (s, e) =>
				{
					this.RefreshTabPointers();
				}; */

				this.RefreshTabPointers();
			}
		}

		public void RefreshTabPointers()
		{
			ChromeTab lastTab = null;

			foreach(var tab in AllTabs.OfType<ChromeTab>().Where(t => t.Visible))
			{
				if (lastTab != null)
				{
					lastTab.NextTab = tab;
				}

				tab.PreviousTab = lastTab;

				lastTab = tab;
			}
		}

		internal override void RemoveTab(ITab tab)
		{
			base.RemoveTab(tab);

			// Update pointers - collapse out removed tab
			if (tab is ChromeTab removedTab)
			{
				var tabA = removedTab.PreviousTab;
				var tabB = removedTab.NextTab;

				if (tabA != null)
				{
					tabA.NextTab = tabB;
				}

				if (tabB != null)
				{
					tabB.PreviousTab = tabA;
				}
			}

			tabTrailer.LastTab = this.AllTabs.LastOrDefault();
		}

		public Func<GuiWidget> NewTabPage { get; set; }

		protected override void OnActiveTabChanged()
		{
			tabTrailer.LastTab = this.AllTabs.LastOrDefault();
			base.OnActiveTabChanged();
		}
	}

	public class SimpleTab : GuiWidget, ITab
	{
		public event EventHandler CloseClicked;

		protected SimpleTabs parentTabControl;

		protected ThemeConfig theme;

		protected TabPill tabPill;

		public GuiWidget TabContent { get; protected set; }

		public string Key { get; }

		private bool hasClose = false;

		public SimpleTab(string tabKey, string tabLabel, SimpleTabs parentTabControl, GuiWidget tabContent, ThemeConfig theme, string tabImageUrl = null, bool hasClose = true, double pointSize = 12, ImageBuffer iconImage = null)
		{
			this.Key = tabKey;
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit | VAnchor.Bottom;
			this.Padding = 0;
			this.Margin = 0;
			this.theme = theme;
			this.hasClose = hasClose;

			this.TabContent = tabContent;
			this.parentTabControl = parentTabControl;

			if (iconImage != null)
			{
				tabPill = new TabPill(tabLabel, theme.TextColor, iconImage, pointSize);
			}
			else
			{
				tabPill = new TabPill(tabLabel, theme.TextColor, tabImageUrl, pointSize);
			}
			tabPill.Margin = (hasClose) ? new BorderDouble(right: 16) : 0;

			this.AddChild(tabPill);

			if (hasClose)
			{
				var closeButton = theme.CreateSmallResetButton();
				closeButton.HAnchor = HAnchor.Right;
				closeButton.Margin = new BorderDouble(right: 7, top: 1);
				closeButton.Name = "Close Tab Button";
				closeButton.ToolTipText = "Close".Localize();
				closeButton.Click += (s, e) => ConditionallyCloseTab();

				this.AddChild(closeButton);
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if(mouseEvent.Button == MouseButtons.Middle
				&& hasClose)
			{
				ConditionallyCloseTab();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			// Clear all listeners
			this.CloseClicked = null;
		}

		private void ConditionallyCloseTab()
		{
			UiThread.RunOnIdle(() =>
			{
				if (this.TabContent is PrinterTabPage printerTab
					&& printerTab.printer.Connection.Printing)
				{
					StyledMessageBox.ShowMessageBox(
						(bool response) =>
						{
							if (response)
							{
								UiThread.RunOnIdle(() =>
								{
									this.parentTabControl.RemoveTab(this);
									this.CloseClicked?.Invoke(this, null);
								});
							}
						},
						"Cancel the current print?".Localize(),
						"Cancel Print?".Localize(),
						StyledMessageBox.MessageType.YES_NO,
						"Cancel Print".Localize(),
						"Continue Printing".Localize());
				}
				else
				{
					this.CloseClicked?.Invoke(this, null);

					// Must be called after CloseClicked otherwise listeners are cleared before event is invoked
					this.parentTabControl?.RemoveTab(this);
				}
			});
		}

		protected class TabPill : FlowLayoutWidget
		{
			private TextWidget label;
			private ImageWidget imageWidget;

			public TabPill(string tabTitle, Color textColor, string imageUrl = null, double pointSize = 12)
				: this (tabTitle, textColor, string.IsNullOrEmpty(imageUrl) ? null : new ImageBuffer(16, 16).CreateScaledImage(GuiWidget.DeviceScale), pointSize)
			{
				if (imageWidget != null
					&& !string.IsNullOrEmpty(imageUrl))
				{
					try
					{
						// TODO: Use caching
						// Attempt to load image
						WebCache.RetrieveImageAsync(imageWidget.Image, imageUrl, true);
					}
					catch { }
				}
			}

			public TabPill(string tabTitle, Color textColor, ImageBuffer imageBuffer = null, double pointSize = 12)
			{
				this.Selectable = false;
				this.Padding = new BorderDouble(10, 5, 10, 4);

				if (imageBuffer != null)
				{
					imageWidget = new ImageWidget(imageBuffer)
					{
						Margin = new BorderDouble(right: 6, bottom: 2),
						VAnchor = VAnchor.Center
					};
					this.AddChild(imageWidget);
				}

				label = new TextWidget(tabTitle, pointSize: pointSize)
				{
					TextColor = textColor,
					VAnchor = VAnchor.Center,
					AutoExpandBoundsToText = true
				};
				this.AddChild(label);
			}

			public Color TextColor
			{
				get => label.TextColor;
				set => label.TextColor = value;
			}

			public override string Text
			{
				get => label.Text;
				set => label.Text = value;
			}
		}
	}

	public class ToolTab : SimpleTab
	{
		public Color InactiveTabColor { get; set; }
		public Color ActiveTabColor { get; set; }

		public override Color BorderColor
		{
			get =>  (this.IsActiveTab) ? theme.PrimaryAccentColor : base.BorderColor;
			set => base.BorderColor = value;
		}

		public ToolTab(string tabKey, string tabLabel, SimpleTabs parentTabControl, GuiWidget tabContent, ThemeConfig theme, string tabImageUrl = null, bool hasClose = true, int pointSize = -1)
			: base(tabKey, tabLabel, parentTabControl, tabContent, theme, tabImageUrl, hasClose, pointSize: (pointSize == -1) ? theme.FontSize10 : pointSize)
		{
			this.Border = new BorderDouble(top: 1);
			this.InactiveTabColor = Color.Transparent;
			this.ActiveTabColor = theme.BackgroundColor;

			tabPill.Padding = tabPill.Padding.Clone(top: 10, bottom: 10);
		}

		private bool IsActiveTab => this == parentTabControl.ActiveTab;

		public override string Text { get => tabPill.Text; set => tabPill.Text = value; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Render(
				new RoundedRect(this.LocalBounds, 0),
				(this.IsActiveTab) ? this.ActiveTabColor : this.InactiveTabColor);

			base.OnDraw(graphics2D);
		}
	}

	public class ChromeTab : SimpleTab
	{
		public ChromeTab(string tabKey, string tabLabel, SimpleTabs parentTabControl, GuiWidget tabContent, ThemeConfig theme, string tabImageUrl = null, bool hasClose = true)
			: base(tabKey, tabLabel, parentTabControl, tabContent, theme, tabImageUrl, hasClose)
		{
		}

		public ChromeTab(string tabKey, string tabLabel, SimpleTabs parentTabControl, GuiWidget tabContent, ThemeConfig theme, ImageBuffer imageBuffer, bool hasClose = true)
			: base(tabKey, tabLabel, parentTabControl, tabContent, theme, iconImage: imageBuffer, hasClose: hasClose)
		{
			this.Text = tabLabel;
		}

		private static int tabInsetDistance = 14 / 2;

		internal ChromeTab NextTab { get; set; }

		internal ChromeTab PreviousTab { get; set; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			var rect = LocalBounds;
			var centerY = rect.YCenter;

			var siblings = this.Parent.Children.OfType<ChromeTab>().ToList();

			int position = siblings.IndexOf(this);

			//MainTab leftSibling = (position > 0) ? siblings[position - 1] : null;
			//MainTab rightSibling = (position < siblings.Count - 1) ? siblings[position + 1] : null;

			var activeTab = parentTabControl.ActiveTab;

			bool isFirstTab = position == 0;
			bool rightSiblingSelected = this.NextTab == activeTab;
			bool leftSiblingSelected = this.PreviousTab == activeTab;

			bool drawLeftTabOverlap = this != activeTab && !isFirstTab;

			// Tab - core
			var tabShape = new VertexStorage();
			tabShape.MoveTo(rect.Left, centerY);
			tabShape.LineTo(rect.Left + tabInsetDistance, rect.Top);
			tabShape.LineTo(rect.Right - tabInsetDistance, rect.Top);
			tabShape.LineTo(rect.Right, centerY);
			if (!rightSiblingSelected)
			{
				tabShape.LineTo(rect.Right, rect.Bottom);
			}
			tabShape.LineTo(rect.Right - tabInsetDistance, rect.Bottom);
			tabShape.LineTo(rect.Left + tabInsetDistance, rect.Bottom);
			if (!drawLeftTabOverlap)
			{
				tabShape.LineTo(rect.Left, rect.Bottom);
			}

			graphics2D.Render(
				tabShape,
				(this == activeTab) ? theme.BackgroundColor : theme.InactiveTabColor);

			if (drawLeftTabOverlap)
			{
				DrawTabLowerLeft(
					graphics2D,
					rect,
					(leftSiblingSelected || this == activeTab) ? theme.BackgroundColor : theme.InactiveTabColor);
			}

			if (rightSiblingSelected)
			{
				DrawTabLowerRight(graphics2D, rect, theme.BackgroundColor);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnClosed(EventArgs e)
		{
			this.parentTabControl = null;
			this.TabContent = null;

			base.OnClosed(e);
		}

		public string Title
		{
			get => tabPill?.Text;
			set
			{
				if (tabPill != null)
				{
					tabPill.Text = value;
				}
			}
		}

		public static void DrawTabLowerRight(Graphics2D graphics2D, RectangleDouble rect, Color color)
		{
			// Tab - right nub
			var tabRight = new VertexStorage();
			tabRight.MoveTo(rect.Right, rect.YCenter);
			tabRight.LineTo(rect.Right, rect.Bottom);
			tabRight.LineTo(rect.Right - tabInsetDistance, rect.Bottom);

			graphics2D.Render(tabRight, color);
		}

		public static void DrawTabLowerLeft(Graphics2D graphics2D, RectangleDouble rect, Color color)
		{
			// Tab - left nub
			var tabLeft = new VertexStorage();
			tabLeft.MoveTo(rect.Left, rect.YCenter);
			tabLeft.LineTo(rect.Left + tabInsetDistance, rect.Bottom);
			tabLeft.LineTo(rect.Left, rect.Bottom);

			graphics2D.Line(rect.Left, rect.YCenter, rect.Left + tabInsetDistance, rect.Bottom, AppContext.Theme.MinimalShade, 1.3);

			graphics2D.Render(tabLeft, color);
		}
	}
}
