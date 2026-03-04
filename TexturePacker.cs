using Godot;
using System;
using System.Collections.Generic;

public partial class TexturePacker : RefCounted
{
    public int MaxAtlasSize { get; set; } = 1024;
    public int Margin { get; set; } = 0;
    public Color BackgroundColor { get; set; } = new Color(0, 0, 0, 0);

    private List<Texture2D> _textures = new List<Texture2D>();
    private List<AtlasTexture> _pendingAtlases = new List<AtlasTexture>();
    private List<Texture2D> _generatedTextures = new List<Texture2D>();

    public AtlasTexture AddTexture(Texture2D tex)
    {
        _textures.Add(tex);
        var at = new AtlasTexture();
        _pendingAtlases.Add(at);
        return at;
    }

    public void RemoveTexture(Texture2D tex)
    {
        int idx = _textures.IndexOf(tex);
        if (idx != -1)
        {
            _textures.RemoveAt(idx);
            _pendingAtlases.RemoveAt(idx);
        }
    }

    public void Clear()
    {
        _textures.Clear();
        _pendingAtlases.Clear();
        _generatedTextures.Clear();
    }

    public int GetGeneratedTextureCount()
    {
        return _generatedTextures.Count;
    }

    public Texture2D GetGeneratedTexture(int index)
    {
        if (index >= 0 && index < _generatedTextures.Count)
        {
            return _generatedTextures[index];
        }
        return null;
    }

    private class PackEntry
    {
        public int Index;
        public Image Image;
        public int Width;
        public int Height;
    }
    
    private class PlacementResult
    {
        public int PageIdx;
        public Rect2 Rect;
    }

    public void Merge()
    {
        _generatedTextures.Clear();
        if (_textures.Count == 0)
            return;

        // 1. Collect images and indices
        var entries = new List<PackEntry>();
        for (int i = 0; i < _textures.Count; i++)
        {
            var tex = _textures[i];
            if (tex == null) continue;
            var img = tex.GetImage();
            if (img == null) continue;
            
            entries.Add(new PackEntry
            {
                Index = i,
                Image = img,
                Width = img.GetWidth(),
                Height = img.GetHeight()
            });
        }

        // 2. Sort by height descending
        entries.Sort((a, b) => b.Height.CompareTo(a.Height));

        // 3. Pack
        var pages = new List<Image>();
        int currentPageIdx = -1;

        int penX = Margin;
        int penY = Margin;
        int rowH = 0;

        var placementResults = new Dictionary<int, PlacementResult>();

        foreach (var entry in entries)
        {
            int w = entry.Width;
            int h = entry.Height;

            if (w > MaxAtlasSize || h > MaxAtlasSize)
            {
                GD.PushWarning($"Texture too large for atlas: {w}x{h}");
                continue;
            }

            if (pages.Count == 0)
            {
                AddNewPage(pages);
                currentPageIdx = 0;
                penX = Margin;
                penY = Margin;
                rowH = 0;
            }

            // Check if fits in current row
            if (penX + w + Margin > MaxAtlasSize)
            {
                penX = Margin;
                penY += rowH + Margin;
                rowH = 0;
            }

            // Check if fits in current page vertically
            if (penY + h + Margin > MaxAtlasSize)
            {
                AddNewPage(pages);
                currentPageIdx++;
                penX = Margin;
                penY = Margin;
                rowH = 0;
            }

            // Place it
            var pageImg = pages[currentPageIdx];
            pageImg.BlitRect(entry.Image, new Rect2I(0, 0, w, h), new Vector2I(penX, penY));

            placementResults[entry.Index] = new PlacementResult
            {
                PageIdx = currentPageIdx,
                Rect = new Rect2(penX, penY, w, h)
            };

            // Advance pen
            penX += w + Margin;
            if (h > rowH)
                rowH = h;
        }

        // 4. Create textures from pages
        foreach (var pageImg in pages)
        {
            _generatedTextures.Add(ImageTexture.CreateFromImage(pageImg));
        }

        // 5. Update AtlasTextures
        for (int i = 0; i < _pendingAtlases.Count; i++)
        {
            var at = _pendingAtlases[i];
            if (placementResults.TryGetValue(i, out var res))
            {
                at.Atlas = _generatedTextures[res.PageIdx];
                at.Region = res.Rect;
            }
            else
            {
                at.Atlas = null;
                at.Region = new Rect2(0, 0, 0, 0);
            }
        }
    }

    private void AddNewPage(List<Image> pages)
    {
        var img = Image.Create(MaxAtlasSize, MaxAtlasSize, false, Image.Format.Rgba8);
        img.Fill(BackgroundColor);
        pages.Add(img);
    }
}
