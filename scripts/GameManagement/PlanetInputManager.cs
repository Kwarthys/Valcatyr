using Godot;
using System;
using System.Collections.Generic;

public partial class PlanetInputManager : Area3D
{
    [Export]
    private Planet planet;

    [Export]
    public GameManager gameManager;
    
    [Export]
    public Camera3D camera {get; set;}

    public override void _UnhandledInput(InputEvent @event)
    {
        _processInput(@event);
    }

    private void _processInput(InputEvent _event)
    {
        if(_event.IsAction("Primary") == false && _event.IsAction("Secondary") == false)
            return; // Only treat mouse. When clicking while holding a keyboard key, an event will be sent for both

        bool primary = Input.IsActionJustPressed("Primary");
        bool secondary = Input.IsActionJustPressed("Secondary");
        if(primary || secondary)
        {
            GameManager.PlanetInteraction interaction = primary ? GameManager.PlanetInteraction.Primary : GameManager.PlanetInteraction.Secondary;

            Vector2 mousePos = GetViewport().GetMousePosition();
            Vector3 from = camera.ProjectRayOrigin(mousePos);
            Vector3 to = from + camera.ProjectRayNormal(mousePos) * 5.0f;
            // this types are aweful so let's use some "var"
            var spaceState = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollideWithAreas = true;
            var result = spaceState.IntersectRay(query);

            if(result.Count == 0)
            {
                gameManager.onPlanetInteraction(interaction, -1);
                return;
            }

            Vector3 worldHitPos = (Vector3)result["position"];
            Vector3 planetLocalHitPos = planet.ToLocal(worldHitPos);
            int nodeIndex = planet.nodeFinder.findNodeIndexAtPosition(planetLocalHitPos);
            gameManager.onPlanetInteraction(interaction, nodeIndex);
        }
    }
}
