using UnityEngine;

[DisallowMultipleComponent]
public class SlimeStickyWall : MonoBehaviour
{
    [SerializeField] private bool canStick = true;
    [SerializeField] private bool canWallJump = true;
    [SerializeField, Min(0f)] private float gripMultiplier = 1f;
    [SerializeField, Min(0f)] private float slideSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float wallJumpUpMultiplier = 1f;
    [SerializeField, Min(0f)] private float wallJumpAwayMultiplier = 1f;

    public bool CanStick => canStick;
    public bool CanWallJump => canStick && canWallJump;
    public float GripMultiplier => Mathf.Max(0f, gripMultiplier);
    public float SlideSpeedMultiplier => Mathf.Max(0f, slideSpeedMultiplier);
    public float WallJumpUpMultiplier => Mathf.Max(0f, wallJumpUpMultiplier);
    public float WallJumpAwayMultiplier => Mathf.Max(0f, wallJumpAwayMultiplier);
}
