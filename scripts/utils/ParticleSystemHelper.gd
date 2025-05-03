extends Node3D

@export var particleSystem : GPUParticles3D

func onSignalReceive():
	queue_free(); # Mark for deletion at end of frame

func _ready() -> void:
	particleSystem.restart();
