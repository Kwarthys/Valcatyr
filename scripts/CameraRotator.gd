extends Node3D

@onready var camera_vertical_helper: Node3D = $CameraVerticalHelper
@export var HORIZONTAL_SPEED = 2.5
@export var VERTICAL_SPEED = 1.5
@export var VERTICAL_MAX_ANGLE = 80
@export var VERTICAL_MIN_ANGLE = -80
@export var AUTO_ROTATE_SPEED = 0.9

var vertical_helper_angle: float = 0.0

var autoRotate = false;

func _process(delta: float) -> void:
	if OS.has_feature("debug") and Input.is_action_just_pressed("AutoRotate"):
		autoRotate = !autoRotate
	handle_horizontal(delta)
	handle_vertical(delta)
	
func handle_horizontal(dt: float) -> void:
	var movement: float = 0.0
	if Input.is_action_pressed("Left"):
		movement -= HORIZONTAL_SPEED * dt
	if Input.is_action_pressed("Right"):
		movement += HORIZONTAL_SPEED * dt
	if autoRotate:
		movement += AUTO_ROTATE_SPEED * dt
	rotate(Vector3.UP, movement)
	
func handle_vertical(dt: float) -> void:
	var movement: float = 0.0
	if Input.is_action_pressed("Up"):
		movement -= VERTICAL_SPEED * dt
	if Input.is_action_pressed("Down"):
		movement += VERTICAL_SPEED * dt
	if movement == 0.0:
		return
	var movement_candidate = movement + vertical_helper_angle
	if movement_candidate < deg_to_rad(VERTICAL_MAX_ANGLE) and movement_candidate > deg_to_rad(VERTICAL_MIN_ANGLE):
		vertical_helper_angle += movement
		camera_vertical_helper.rotate_object_local(Vector3.RIGHT, movement)
