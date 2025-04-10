extends Node3D
var wobbleOffset
var accumulatedDT = 0.0
@export var frequency = 1.0
@export var amplitude = 1.0
func _ready():
	wobbleOffset = position

func _process(dt):
	accumulatedDT += dt
	position = wobbleOffset + Vector3.UP * cos(accumulatedDT * TAU * frequency) * amplitude
