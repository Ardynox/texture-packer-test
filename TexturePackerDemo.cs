using Godot;
using System;
using System.Collections.Generic;

public partial class TexturePackerDemo : Control
{
    // Node references
    private TextureRect _atlasView;
    private Label _statusLabel;
    private Label _atlasInfoLabel;
    private GridContainer _iconGrid;
    private Button _btnAdd;
    private Button _btnAddBatch;
    private Button _btnRemove;
    private Button _btnMerge;
    private Button _btnClear;
    private HSlider _sizeSlider;
    private Label _sizeLabel;
    private HSlider _marginSlider;
    private Label _marginLabel;

    // TexturePacker instance
    private TexturePacker _packer;

    // Internal state
    private class Entry
    {
        public string Path;
        public Texture2D Tex;
        public AtlasTexture Atlas;
        public TextureRect Rect;
    }
    private List<Entry> _entries = new List<Entry>();

    private readonly string[] DEMO_TEXTURES = new string[]
    {
        "res://icon.svg",
    };

    public override void _Ready()
    {
        // Initialize node references
        _atlasView = GetNode<TextureRect>("VBox/MainArea/AtlasPanel/AtlasView");
        _statusLabel = GetNode<Label>("VBox/TopBar/StatusLabel");
        _atlasInfoLabel = GetNode<Label>("VBox/TopBar/AtlasInfoLabel");
        _iconGrid = GetNode<GridContainer>("VBox/MainArea/IconPanel/IconScroll/IconGrid");
        _btnAdd = GetNode<Button>("VBox/Toolbar/BtnAdd");
        _btnAddBatch = GetNode<Button>("VBox/Toolbar/BtnAddBatch");
        _btnRemove = GetNode<Button>("VBox/Toolbar/BtnRemove");
        _btnMerge = GetNode<Button>("VBox/Toolbar/BtnMerge");
        _btnClear = GetNode<Button>("VBox/Toolbar/BtnClear");
        _sizeSlider = GetNode<HSlider>("VBox/Toolbar/SizeSlider");
        _sizeLabel = GetNode<Label>("VBox/Toolbar/SizeLabel");
        _marginSlider = GetNode<HSlider>("VBox/Toolbar/MarginSlider");
        _marginLabel = GetNode<Label>("VBox/Toolbar/MarginLabel");

        _packer = new TexturePacker();
        ApplyPackerSettings();

        _btnAdd.Pressed += OnBtnAddPressed;
        _btnAddBatch.Pressed += OnBtnAddBatchPressed;
        _btnRemove.Pressed += OnBtnRemovePressed;
        _btnMerge.Pressed += OnBtnMergePressed;
        _btnClear.Pressed += OnBtnClearPressed;
        _sizeSlider.ValueChanged += OnSizeSliderChanged;
        _marginSlider.ValueChanged += OnMarginSliderChanged;

        UpdateStatus("就绪。点击「添加纹理」开始。");
        UpdateLabels();

        foreach (var path in DEMO_TEXTURES)
        {
            if (ResourceLoader.Exists(path))
            {
                AddTextureFromPath(path);
            }
        }
        DoMerge();
    }

    private void OnBtnAddPressed()
    {
        var dialog = new FileDialog();
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Filters = new string[] { "*.png,*.jpg,*.svg,*.webp ; 图片文件" };
        dialog.Access = FileDialog.AccessEnum.Resources;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(800, 500));

        dialog.FileSelected += (path) =>
        {
            AddTextureFromPath(path);
            DoMerge();
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
    }

    private void OnBtnAddBatchPressed()
    {
        var dialog = new FileDialog();
        dialog.FileMode = FileDialog.FileModeEnum.OpenFiles;
        dialog.Filters = new string[] { "*.png,*.jpg,*.svg,*.webp ; 图片文件" };
        dialog.Access = FileDialog.AccessEnum.Resources;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(800, 500));

        dialog.FilesSelected += (paths) =>
        {
            foreach (var path in paths)
            {
                AddTextureFromPath(path);
            }
            DoMerge();
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
    }

    private void OnBtnRemovePressed()
    {
        if (_entries.Count == 0)
        {
            UpdateStatus("没有可移除的纹理。");
            return;
        }

        var last = _entries[_entries.Count - 1];
        _packer.RemoveTexture(last.Tex);
        last.Rect.QueueFree();
        _entries.RemoveAt(_entries.Count - 1);
        UpdateStatus($"已移除：{System.IO.Path.GetFileName(last.Path)}");
        DoMerge();
    }

    private void OnBtnMergePressed()
    {
        DoMerge();
    }

    private void OnBtnClearPressed()
    {
        _packer.Clear();
        foreach (var e in _entries)
        {
            e.Rect.QueueFree();
        }
        _entries.Clear();
        _atlasView.Texture = null;
        UpdateStatus("已清空所有纹理。");
        UpdateLabels();
    }

    private void OnSizeSliderChanged(double value)
    {
        _packer.MaxAtlasSize = (int)value;
        UpdateLabels();
    }

    private void OnMarginSliderChanged(double value)
    {
        _packer.Margin = (int)value;
        UpdateLabels();
    }

    private void AddTextureFromPath(string path)
    {
        var tex = GD.Load<Texture2D>(path);
        if (tex == null)
        {
            UpdateStatus($"加载失败：{path}");
            return;
        }

        var atlasTex = _packer.AddTexture(tex);

        var texRect = new TextureRect();
        texRect.Texture = atlasTex;
        texRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        texRect.TooltipText = System.IO.Path.GetFileName(path);
        _iconGrid.AddChild(texRect);

        _entries.Add(new Entry
        {
            Path = path,
            Tex = tex,
            Atlas = atlasTex,
            Rect = texRect
        });

        UpdateStatus($"已添加：{System.IO.Path.GetFileName(path)}（共 {_entries.Count} 张）");
    }

    private void DoMerge()
    {
        if (_entries.Count == 0)
        {
            _atlasView.Texture = null;
            UpdateLabels();
            return;
        }

        ApplyPackerSettings();
        _packer.Merge();

        int count = _packer.GetGeneratedTextureCount();
        if (count > 0)
        {
            _atlasView.Texture = _packer.GetGeneratedTexture(0);
        }

        UpdateLabels();
        UpdateStatus($"打包完成：{_entries.Count} 张纹理 → {count} 个图集 bin");

        // Refresh atlas textures
        foreach (var e in _entries)
        {
            e.Rect.Texture = null;
            e.Rect.Texture = e.Atlas;
        }
    }

    private void ApplyPackerSettings()
    {
        _packer.MaxAtlasSize = (int)_sizeSlider.Value;
        _packer.Margin = (int)_marginSlider.Value;
        _packer.BackgroundColor = new Color(0, 0, 0, 0);
    }

    private void UpdateStatus(string msg)
    {
        _statusLabel.Text = msg;
    }

    private void UpdateLabels()
    {
        _sizeLabel.Text = $"图集上限: {(int)_sizeSlider.Value}";
        _marginLabel.Text = $"间距: {(int)_marginSlider.Value}";

        int count = _packer.GetGeneratedTextureCount();
        if (count > 0)
        {
            var sz = _packer.GetGeneratedTexture(0).GetSize();
            _atlasInfoLabel.Text = $"图集 0: {sz.X:F0}x{sz.Y:F0}  |  共 {count} bin";
        }
        else
        {
            _atlasInfoLabel.Text = "尚未生成图集";
        }
    }
}
