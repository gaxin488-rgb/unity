using UnityEngine;

// ============================================================================
//  PIPELINE B — STAGE 2: USE PLACEHOLDER OBJECTS
//  Assets/_Project/Code/World/PlaceholderObjectMarker.cs
//
//  Tags primitive greybox / blockout objects (Ramps, Stepping Pillars, Crates,
//  Cover Walls, Combat Dummies) used to prototype scale, jump arcs, sightlines,
//  and acoustic surfaces in Verda Wilds.
// ============================================================================
namespace EchoesOfTheLost.World
{
    public enum PlaceholderArchetype
    {
        TraversalRamp = 0,
        SteppingPillar = 1,
        InteractableCrate = 2,
        TacticalCoverWall = 3,
        CombatTargetDummy = 4
    }

    [DisallowMultipleComponent]
    public class PlaceholderObjectMarker : MonoBehaviour
    {
        [Header("Placeholder Blockout Metadata")]
        [SerializeField] private string _blockoutLabel = "PROTO_BLOCK_01";
        [SerializeField] private PlaceholderArchetype _archetype = PlaceholderArchetype.SteppingPillar;
        [SerializeField] private Vector3 _designDimensionsMeters = Vector3.one;
        [SerializeField] private Color _gizmoWireColor = new Color(1.0f, 0.72f, 0.18f, 0.9f);
        [SerializeField] private string _surfaceAudioTag = "Stone";

        public string BlockoutLabel => _blockoutLabel;
        public PlaceholderArchetype Archetype => _archetype;
        public Vector3 DesignDimensionsMeters => _designDimensionsMeters;
        public string SurfaceAudioTag => _surfaceAudioTag;

        public void ConfigurePlaceholder(
            string label,
            PlaceholderArchetype archetype,
            Vector3 dimensions,
            string surfaceTag,
            Color wireColor)
        {
            _blockoutLabel = label;
            _archetype = archetype;
            _designDimensionsMeters = dimensions;
            _surfaceAudioTag = surfaceTag;
            _gizmoWireColor = wireColor;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _gizmoWireColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 1.02f);
        }
    }
}
