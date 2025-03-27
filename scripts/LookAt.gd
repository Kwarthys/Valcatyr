extends Node3D

func _ready() -> void:
	transform = transform.looking_at(Vector3.ZERO)
