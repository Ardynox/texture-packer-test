extends Control
# ================================================================
# TexturePackerDemo.gd
# 挂载到场景根节点（Control）
# ================================================================

# ── 节点引用（对应场景树）──────────────────────────────────────
@onready var atlas_view       : TextureRect   = $VBox/MainArea/AtlasPanel/AtlasView
@onready var status_label     : Label         = $VBox/TopBar/StatusLabel
@onready var atlas_info_label : Label         = $VBox/TopBar/AtlasInfoLabel
@onready var icon_grid        : GridContainer = $VBox/MainArea/IconPanel/IconScroll/IconGrid
@onready var btn_add          : Button        = $VBox/Toolbar/BtnAdd
@onready var btn_add_batch    : Button        = $VBox/Toolbar/BtnAddBatch
@onready var btn_remove       : Button        = $VBox/Toolbar/BtnRemove
@onready var btn_merge        : Button        = $VBox/Toolbar/BtnMerge
@onready var btn_clear        : Button        = $VBox/Toolbar/BtnClear
@onready var size_slider      : HSlider       = $VBox/Toolbar/SizeSlider
@onready var size_label       : Label         = $VBox/Toolbar/SizeLabel
@onready var margin_slider    : HSlider       = $VBox/Toolbar/MarginSlider
@onready var margin_label     : Label         = $VBox/Toolbar/MarginLabel

# ── TexturePacker 实例 ──────────────────────────────────────────
var packer : RefCounted
const TexturePackerScript = preload("res://packer.gd")

# ── 内部状态 ────────────────────────────────────────────────────
# 每条记录：{ "path": String, "tex": Texture2D, "atlas": AtlasTexture, "rect": TextureRect }
var entries : Array[Dictionary] = []

const DEMO_TEXTURES : Array[String] = [
	"res://icon.svg",
]


# ================================================================
func _ready() -> void:
	packer = TexturePackerScript.new()
	_apply_packer_settings()

	btn_add.pressed.connect(_on_btn_add_pressed)
	btn_add_batch.pressed.connect(_on_btn_add_batch_pressed)
	btn_remove.pressed.connect(_on_btn_remove_pressed)
	btn_merge.pressed.connect(_on_btn_merge_pressed)
	btn_clear.pressed.connect(_on_btn_clear_pressed)
	size_slider.value_changed.connect(_on_size_slider_changed)
	margin_slider.value_changed.connect(_on_margin_slider_changed)

	_update_status("就绪。点击「添加纹理」开始。")
	_update_labels()

	for path in DEMO_TEXTURES:
		if ResourceLoader.exists(path):
			_add_texture_from_path(path)
	_do_merge()


# ================================================================
# ── 按钮回调 ────────────────────────────────────────────────────

func _on_btn_add_pressed() -> void:
	var dialog := FileDialog.new()
	dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	dialog.filters = ["*.png,*.jpg,*.svg,*.webp ; 图片文件"]
	dialog.access = FileDialog.ACCESS_RESOURCES
	add_child(dialog)
	dialog.popup_centered(Vector2i(800, 500))

	# ★ 修复：await 返回 Variant，用中间变量 + 显式 String() 转型
	var result : Variant = await dialog.file_selected
	var selected_path : String = String(result)
	dialog.queue_free()

	if selected_path.is_empty():
		return
	_add_texture_from_path(selected_path)
	_do_merge()


func _on_btn_add_batch_pressed() -> void:
	var dialog := FileDialog.new()
	dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILES
	dialog.filters = ["*.png,*.jpg,*.svg,*.webp ; 图片文件"]
	dialog.access = FileDialog.ACCESS_RESOURCES
	add_child(dialog)
	dialog.popup_centered(Vector2i(800, 500))

	var result : Variant = await dialog.files_selected
	var selected_paths : PackedStringArray = result
	dialog.queue_free()

	if selected_paths.is_empty():
		return
	
	for path in selected_paths:
		_add_texture_from_path(path)
	
	_do_merge()


func _on_btn_remove_pressed() -> void:
	if entries.is_empty():
		_update_status("没有可移除的纹理。")
		return

	# ★ 修复：entries.back() 返回 Variant，显式声明为 Dictionary
	var last : Dictionary = entries.back()
	packer.remove_texture(last["tex"] as Texture2D)
	(last["rect"] as TextureRect).queue_free()
	entries.pop_back()
	_update_status("已移除：%s" % (last["path"] as String).get_file())
	_do_merge()


func _on_btn_merge_pressed() -> void:
	_do_merge()


func _on_btn_clear_pressed() -> void:
	packer.clear()
	for e : Dictionary in entries:
		(e["rect"] as TextureRect).queue_free()
	entries.clear()
	atlas_view.texture = null
	_update_status("已清空所有纹理。")
	_update_labels()


func _on_size_slider_changed(value: float) -> void:
	packer.max_atlas_size = int(value)
	_update_labels()


func _on_margin_slider_changed(value: float) -> void:
	packer.margin = int(value)
	_update_labels()


# ================================================================
# ── 核心逻辑 ────────────────────────────────────────────────────

func _add_texture_from_path(path: String) -> void:
	var tex := load(path) as Texture2D
	if tex == null:
		_update_status("加载失败：%s" % path)
		return

	var atlas_tex : AtlasTexture = packer.add_texture(tex)

	var tex_rect := TextureRect.new()
	tex_rect.texture = atlas_tex
	tex_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	tex_rect.tooltip_text = path.get_file()
	icon_grid.add_child(tex_rect)

	entries.append({
		"path":  path,
		"tex":   tex,
		"atlas": atlas_tex,
		"rect":  tex_rect,
	})
	_update_status("已添加：%s（共 %d 张）" % [path.get_file(), entries.size()])


func _do_merge() -> void:
	if entries.is_empty():
		atlas_view.texture = null
		_update_labels()
		return

	_apply_packer_settings()
	packer.merge()

	var count : int = packer.get_generated_texture_count()
	if count > 0:
		atlas_view.texture = packer.get_generated_texture(0)

	_update_labels()
	_update_status("打包完成：%d 张纹理 → %d 个图集 bin" % [entries.size(), count])

	# ★ 修复：字典取值是 Variant，用 as 显式转型
	for e : Dictionary in entries:
		var tex_rect : TextureRect = e["rect"] as TextureRect
		var at : AtlasTexture = e["atlas"] as AtlasTexture
		tex_rect.texture = null
		tex_rect.texture = at


func _apply_packer_settings() -> void:
	packer.max_atlas_size = int(size_slider.value)
	packer.margin = int(margin_slider.value)
	packer.background_color = Color(0, 0, 0, 0)


func _update_status(msg: String) -> void:
	status_label.text = msg


func _update_labels() -> void:
	size_label.text   = "图集上限: %d" % int(size_slider.value)
	margin_label.text = "间距: %d" % int(margin_slider.value)

	var count : int = packer.get_generated_texture_count()
	if count > 0:
		var sz : Vector2 = packer.get_generated_texture(0).get_size()
		atlas_info_label.text = "图集 0: %.0fx%.0f  |  共 %d bin" % [sz.x, sz.y, count]
	else:
		atlas_info_label.text = "尚未生成图集"
