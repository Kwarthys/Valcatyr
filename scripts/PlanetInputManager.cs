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

    public override void _PhysicsProcess(double _dt)
    {
        if(FreeMovementManager.Instance.isInteractionOn())
            return; // don't do raycast stuff if we're clicking on some menu

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
