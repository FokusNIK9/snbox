using Sandbox;
using Sandbox.Citizen;

public sealed class PlayerUnitController : Component
{
    [Property] public CharacterController Controller { get; set; }
    [Property] public CitizenAnimationHelper AnimationHelper { get; set; }
    [Property] public float MoveSpeed { get; set; } = 300f;

    [Property] public GameObject CursorObject { get; set; } 
    
    [Sync] public Vector3 NetCursorPosition { get; set; } 
    
    private ModelRenderer _cursorRenderer;

    protected override void OnStart()
    {
        if ( Controller == null ) Controller = Components.Get<CharacterController>();
        
        if ( AnimationHelper == null )
            AnimationHelper = Components.GetInChildren<CitizenAnimationHelper>();

        if ( AnimationHelper != null && AnimationHelper.Target == null )
        {
            AnimationHelper.Target = AnimationHelper.Components.Get<SkinnedModelRenderer>();
        }
    }

    protected override void OnUpdate()
    {
        if ( Controller == null ) return;

        // ── Owner-only: input, cursor, physics, rotation ──
        Vector3 wishVelocity = Vector3.Zero;
        if ( !IsProxy )
        {
            UpdateCursorPosition();

            var moveInput = Input.AnalogMove;
            var camRot = Scene.Camera.WorldRotation;
            var moveDir = camRot * new Vector3(moveInput.x, moveInput.y, 0);
            moveDir.z = 0; 
            
            wishVelocity = moveDir.Normal * MoveSpeed;

            Controller.Accelerate(wishVelocity);
            Controller.ApplyFriction(4f);

            if (!Controller.IsOnGround)
                Controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
            else
                Controller.Velocity = Controller.Velocity.WithZ(0);

            Controller.Move();

            var lookDir = (NetCursorPosition - WorldPosition).WithZ(0).Normal;
            if ( lookDir.LengthSquared > 0.01f )
            {
                WorldRotation = Rotation.Slerp(WorldRotation, Rotation.LookAt(lookDir, Vector3.Up), Time.Delta * 15f);
            }
        }

        // ── All clients: cursor visual ──
        if ( CursorObject != null )
        {
            var targetPos = NetCursorPosition.WithZ( 5f );
            CursorObject.WorldPosition = Vector3.Lerp(CursorObject.WorldPosition, targetPos, Time.Delta * 25f);
            
            if ( _cursorRenderer == null )
                _cursorRenderer = CursorObject.Components.Get<ModelRenderer>();
                
            if ( _cursorRenderer != null )
            {
                float dist = Vector3.DistanceBetween( WorldPosition.WithZ(0), NetCursorPosition.WithZ(0) );
                
                float targetAlpha = 1f;
                if ( dist < 120f )
                {
                    targetAlpha = 0.15f + (dist / 120f) * 0.85f;
                }
                
                var color = _cursorRenderer.Tint;
                color.a += (targetAlpha - color.a) * Time.Delta * 15f; 
                _cursorRenderer.Tint = color;
            }
        }

        // ── All clients: animation from synced data (Rule 2.2) ──
        if (AnimationHelper != null)
        {
            var animLookDir = (NetCursorPosition - WorldPosition).WithZ(0).Normal;

            AnimationHelper.WithVelocity(Controller.Velocity);
            AnimationHelper.WithWishVelocity(wishVelocity); 
            AnimationHelper.IsGrounded = Controller.IsOnGround;
            AnimationHelper.WithLook(animLookDir, 1f, 1f, 0.5f); 
        }
    }

    private void UpdateCursorPosition()
    {
        var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );
        
        var tr = Scene.Trace.Ray( ray, 10000f )
            .WithoutTags( "player" )
            .Run();
            
        if ( tr.Hit )
        {
            NetCursorPosition = tr.HitPosition;
        }
        else
        {
            if ( System.Math.Abs( ray.Forward.z ) > 0.001f )
            {
                float distance = -ray.Position.z / ray.Forward.z;
                if ( distance > 0 )
                {
                    NetCursorPosition = ray.Position + ray.Forward * distance;
                }
            }
        }
    }
}
