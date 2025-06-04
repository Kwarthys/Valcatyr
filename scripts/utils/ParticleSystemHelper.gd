extends Node3D

@export var particleSystem : GPUParticles3D
@export var explosionSoundPlayer: AudioStreamPlayer3D

const sound1 : AudioStreamMP3 = preload("res://assets/bomb-explosion-sfx-352274.mp3") # Royalty free sound effects
const sound2 : AudioStreamMP3 = preload("res://assets/medium-explosion-40472.mp3")

@export var pitchRange : float = 0.5

var completionRecieved : int = 0

func onSignalReceive():
	completionRecieved += 1
	if completionRecieved >= 2:
		queue_free(); # Mark for deletion at end of frame when sound AND particle System have finished

func _ready() -> void:
	particleSystem.restart()
	_randomizeSound()
	explosionSoundPlayer.play()
	
func _randomizeSound() -> void:
	explosionSoundPlayer.stream = sound1 if randf() > 0.5 else sound2 # wow weird ternary for: randf > 0.5 ? s1 : s2
	explosionSoundPlayer.pitch_scale = lerp(1.0 - pitchRange, 1.0 + pitchRange, randf())
