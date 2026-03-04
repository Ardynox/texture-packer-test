extends RefCounted

var max_atlas_size : int = 1024
var margin : int = 0
var background_color : Color = Color(0, 0, 0, 0)

var _textures : Array[Texture2D] = []
var _pending_atlases : Array[AtlasTexture] = []
var _generated_textures : Array[Texture2D] = []

func add_texture(tex: Texture2D) -> AtlasTexture:
	_textures.append(tex)
	var at := AtlasTexture.new()
	_pending_atlases.append(at)
	return at

func remove_texture(tex: Texture2D) -> void:
	var idx = _textures.find(tex)
	if idx != -1:
		_textures.remove_at(idx)
		_pending_atlases.remove_at(idx)

func clear() -> void:
	_textures.clear()
	_pending_atlases.clear()
	_generated_textures.clear()

func get_generated_texture_count() -> int:
	return _generated_textures.size()

func get_generated_texture(index: int) -> Texture2D:
	if index >= 0 and index < _generated_textures.size():
		return _generated_textures[index]
	return null

func merge() -> void:
	_generated_textures.clear()
	if _textures.is_empty():
		return
		
	# 1. Collect images and indices
	var entries = []
	for i in range(_textures.size()):
		var tex = _textures[i]
		if not tex: continue
		var img = tex.get_image()
		if not img: continue
		entries.append({
			"index": i,
			"image": img,
			"width": img.get_width(),
			"height": img.get_height()
		})
	
	# 2. Sort by height descending (simple heuristic)
	entries.sort_custom(func(a, b): return a.height > b.height)
	
	# 3. Pack
	var pages : Array[Image] = []
	var current_page_idx = -1
	
	# Current placement state
	var pen_x = margin
	var pen_y = margin
	var row_h = 0
	
	# Results
	var placement_results = {} # original_index -> { page_idx, rect }
	
	for entry in entries:
		var w = entry.width
		var h = entry.height
		
		# If too big for atlas, skip or warn (here we skip or it will just fail to pack)
		if w > max_atlas_size or h > max_atlas_size:
			push_warning("Texture too large for atlas: " + str(w) + "x" + str(h))
			continue
			
		# Check if we need a new page (first run or full)
		if pages.is_empty():
			_add_new_page(pages)
			current_page_idx = 0
			pen_x = margin
			pen_y = margin
			row_h = 0
			
		# Check if fits in current row
		if pen_x + w + margin > max_atlas_size:
			# Move to next row
			pen_x = margin
			pen_y += row_h + margin
			row_h = 0
			
		# Check if fits in current page vertically
		if pen_y + h + margin > max_atlas_size:
			# New page
			_add_new_page(pages)
			current_page_idx += 1
			pen_x = margin
			pen_y = margin
			row_h = 0
			
		# Place it
		var page_img : Image = pages[current_page_idx]
		page_img.blit_rect(entry.image, Rect2i(0, 0, w, h), Vector2i(pen_x, pen_y))
		
		placement_results[entry.index] = {
			"page_idx": current_page_idx,
			"rect": Rect2(pen_x, pen_y, w, h)
		}
		
		# Advance pen
		pen_x += w + margin
		if h > row_h:
			row_h = h
			
	# 4. Create textures from pages
	for page_img in pages:
		_generated_textures.append(ImageTexture.create_from_image(page_img))
		
	# 5. Update AtlasTextures
	for i in range(_pending_atlases.size()):
		var at = _pending_atlases[i]
		if i in placement_results:
			var res = placement_results[i]
			at.atlas = _generated_textures[res.page_idx]
			at.region = res.rect
		else:
			# Not packed (too big or error)
			at.atlas = null
			at.region = Rect2(0,0,0,0)

func _add_new_page(pages: Array[Image]) -> void:
	var img = Image.create(max_atlas_size, max_atlas_size, false, Image.FORMAT_RGBA8)
	img.fill(background_color)
	pages.append(img)
