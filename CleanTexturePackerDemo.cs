using Godot;
using System;
using System.Collections.Generic;

public partial class CleanTexturePackerDemo : Control
{
	// ========================================================================
	// Nodes
	// ========================================================================
	[ExportCategory("UI References")]
	[Export] private TextureRect _atlasView;
	[Export] private Label _statusLabel;
	[Export] private Label _atlasInfoLabel;
	[Export] private HFlowContainer _iconGrid;
	
	[ExportGroup("Toolbar")]
	[Export] private Button _btnAdd;
	[Export] private Button _btnAddBatch;
	[Export] private Button _btnRemove;
	[Export] private Button _btnMerge;
	[Export] private Button _btnClear;
	[Export] private HSlider _sizeSlider;
	[Export] private Label _sizeLabel;
	[Export] private HSlider _marginSlider;
	[Export] private Label _marginLabel;

	// ========================================================================
	// Data & State
	// ========================================================================
	private TexturePacker _packer;
	private readonly List<TextureEntry> _entries = new();
	
	private class TextureEntry
	{
		public string Path;
		public Texture2D Texture;
		public AtlasTexture AtlasTexture;
		public TextureRect UIControl;
	}

	// ========================================================================
	// Lifecycle
	// ========================================================================
	public override void _Ready()
	{
		InitializePacker();
		ConnectSignals();
		
		UpdateStatus("就绪。");
		UpdateSettingsLabels();
		
		// Load default icon for demonstration
		if (ResourceLoader.Exists("res://icon.svg"))
		{
			AddTexture("res://icon.svg");
			PerformMerge();
		}
	}

	private void InitializePacker()
	{
		_packer = new TexturePacker();
		ApplySettingsToPacker();
	}

	private void ConnectSignals()
	{
		_btnAdd.Pressed += OnAddPressed;
		_btnAddBatch.Pressed += OnAddBatchPressed;
		_btnRemove.Pressed += OnRemovePressed;
		_btnMerge.Pressed += OnMergePressed;
		_btnClear.Pressed += OnClearPressed;

		_sizeSlider.ValueChanged += (v) => { ApplySettingsToPacker(); UpdateSettingsLabels(); };
		_marginSlider.ValueChanged += (v) => { ApplySettingsToPacker(); UpdateSettingsLabels(); };
	}

	// ========================================================================
	// Event Handlers
	// ========================================================================
	private void OnAddPressed()
	{
		ShowFileDialog(FileDialog.FileModeEnum.OpenFile, (paths) => 
		{
			if (paths.Length > 0) AddTexture(paths[0]);
			PerformMerge();
		});
	}

	private void OnAddBatchPressed()
	{
		ShowFileDialog(FileDialog.FileModeEnum.OpenFiles, (paths) => 
		{
			foreach (var p in paths) AddTexture(p);
			PerformMerge();
		});
	}

	private void OnRemovePressed()
	{
		if (_entries.Count == 0) return;

		var lastEntry = _entries[^1]; // Index from end
		_packer.RemoveTexture(lastEntry.Texture);
		lastEntry.UIControl.QueueFree();
		_entries.RemoveAt(_entries.Count - 1);

		UpdateStatus($"已移除: {System.IO.Path.GetFileName(lastEntry.Path)}");
		PerformMerge();
	}

	private void OnMergePressed() => PerformMerge();

	private void OnClearPressed()
	{
		_packer.Clear();
		foreach (var e in _entries) e.UIControl.QueueFree();
		_entries.Clear();
		_atlasView.Texture = null;
		
		UpdateStatus("已清空");
		UpdateAtlasInfoLabel();
	}

	// ========================================================================
	// Core Logic
	// ========================================================================
	private void AddTexture(string path)
	{
		var tex = GD.Load<Texture2D>(path);
		if (tex == null)
		{
			GD.PushWarning($"Failed to load texture: {path}");
			return;
		}

		var atlasTex = _packer.AddTexture(tex);
		var uiControl = CreateTextureControl(atlasTex, path);
		if (_iconGrid != null)
		{
			_iconGrid.AddChild(uiControl);
		}

		_entries.Add(new TextureEntry
		{
			Path = path,
			Texture = tex,
			AtlasTexture = atlasTex,
			UIControl = uiControl
		});

		UpdateStatus($"已添加: {System.IO.Path.GetFileName(path)}");
	}

	private void PerformMerge()
	{
		if (_entries.Count == 0)
		{
			_atlasView.Texture = null;
			UpdateAtlasInfoLabel();
			return;
		}

		ApplySettingsToPacker();
		_packer.Merge();

		// Display the first page of the atlas
		if (_packer.GetGeneratedTextureCount() > 0)
		{
			_atlasView.Texture = _packer.GetGeneratedTexture(0);
		}

		// Refresh all atlas textures in the grid (they might have changed regions)
		foreach (var e in _entries)
		{
			// Trigger a redraw or update if necessary; 
			// AtlasTexture automatically updates if its Atlas/Region properties change.
			// But sometimes assigning null then back helps if it gets stuck (rare in Godot 4).
			// e.UIControl.Texture = null; 
			e.UIControl.Texture = e.AtlasTexture;
		}

		UpdateAtlasInfoLabel();
		UpdateStatus($"打包完成 - {_entries.Count} 张图片");
	}

	// ========================================================================
	// UI Helpers
	// ========================================================================
	private TextureRect CreateTextureControl(Texture2D tex, string tooltip)
	{
		return new TextureRect
		{
			Texture = tex,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TooltipText = System.IO.Path.GetFileName(tooltip)
			// Layout is handled by the GridContainer, MinSize is optional/auto
		};
	}

	private void ShowFileDialog(FileDialog.FileModeEnum mode, Action<string[]> onSelected)
	{
		var dialog = new FileDialog
		{
			FileMode = mode,
			Access = FileDialog.AccessEnum.Resources,
			Filters = new[] { "*.png, *.jpg, *.jpeg, *.svg, *.webp; Image Files" },
			MinSize = new Vector2I(800, 600)
		};
		
		AddChild(dialog);
		dialog.PopupCentered();

		dialog.FileSelected += (path) => { onSelected(new[] { path }); dialog.QueueFree(); };
		dialog.FilesSelected += (paths) => { onSelected(paths); dialog.QueueFree(); };
		dialog.Canceled += () => dialog.QueueFree();
	}

	private void ApplySettingsToPacker()
	{
		_packer.MaxAtlasSize = (int)_sizeSlider.Value;
		_packer.Margin = (int)_marginSlider.Value;
	}

	private void UpdateStatus(string msg) => _statusLabel.Text = msg;

	private void UpdateSettingsLabels()
	{
		_sizeLabel.Text = $"图集上限: {_sizeSlider.Value}";
		_marginLabel.Text = $"间距: {_marginSlider.Value}";
	}

	private void UpdateAtlasInfoLabel()
	{
		int count = _packer.GetGeneratedTextureCount();
		if (count > 0)
		{
			var size = _packer.GetGeneratedTexture(0).GetSize();
			_atlasInfoLabel.Text = $"图集: {size.X}x{size.Y} ({count} 页)";
		}
		else
		{
			_atlasInfoLabel.Text = "无图集";
		}
	}
}
